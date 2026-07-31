namespace CatVM.Metal.Hardware;

/// <summary>
/// Discovery of the host's real block devices, done entirely through sysfs and procfs so that no
/// libc interop is needed (which keeps this working under Native AOT on musl).
/// </summary>
public static class BlockDevices {
    private const string SysBlock = "/sys/block";
    private const string SysClassBlock = "/sys/class/block";
    private const long SectorSize = 512;

    /// <summary>
    /// Kernel device names that are never real disks, or that would be pointless (and in the case of
    /// loop/dm, dangerous) to hand to the guest.
    /// </summary>
    private static readonly string[] IgnoredPrefixes = [
        "loop", "ram", "zram", "zd", "nbd", "dm-", "md", "sr", "fd", "dasd"
    ];

    /// <summary>
    /// Finds every block device that is safe to give to the guest: a whole disk with media in it,
    /// that the host itself is not using. The disk the machine booted from is therefore excluded,
    /// as is anything else that is mounted or in use as swap.
    /// </summary>
    /// <param name="excludedPaths">Extra device paths to leave out.</param>
    /// <returns>The usable devices, ordered by kernel name so ports are stable across boots.</returns>
    public static List<BlockDeviceInfo> Discover(IEnumerable<string> excludedPaths) {
        List<BlockDeviceInfo> found = [];

        if (!Directory.Exists(SysBlock)) {
            Log.Warn($"{SysBlock} is not present, no disks can be discovered");
            return found;
        }

        HashSet<string> inUse = GetDisksInUseByHost();
        HashSet<string> excluded = [];
        foreach (string path in excludedPaths) {
            excluded.Add(ResolveDeviceName(path));
        }

        foreach (string directory in Directory.EnumerateDirectories(SysBlock)) {
            string name = Path.GetFileName(directory);

            if (IsIgnoredName(name)) {
                continue;
            }

            string path = $"/dev/{name}";
            if (!File.Exists(path)) {
                Log.Warn($"skipping {name}: {path} does not exist");
                continue;
            }

            if (inUse.Contains(name)) {
                Log.Info($"skipping {path}: in use by the host (this is where CatVM itself is running from)");
                continue;
            }

            if (excluded.Contains(name)) {
                Log.Info($"skipping {path}: excluded");
                continue;
            }

            if (ReadFlag(Path.Combine(directory, "ro"))) {
                Log.Info($"skipping {path}: read only");
                continue;
            }

            long size = ReadSize(Path.Combine(directory, "size"));
            if (size <= 0) {
                Log.Info($"skipping {path}: no media");
                continue;
            }

            found.Add(new BlockDeviceInfo(path, name, size, ReadModel(directory),
                ReadFlag(Path.Combine(directory, "removable"))));
        }

        found.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return found;
    }

    /// <summary>
    /// Describes a device given explicitly on the command line. No safety checks are applied; a
    /// path passed by hand is taken as the operator's decision.
    /// </summary>
    public static BlockDeviceInfo Describe(string path) {
        string name = ResolveDeviceName(path);
        string directory = Path.Combine(SysClassBlock, name);

        if (Directory.Exists(directory)) {
            return new BlockDeviceInfo(path, name, ReadSize(Path.Combine(directory, "size")),
                ReadModel(directory), ReadFlag(Path.Combine(directory, "removable")));
        }

        // Not a block device: most likely an image file, which is how the machine gets tested
        // without real hardware.
        long size = 0;
        try {
            size = new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            // Leave the size unknown; opening it will produce the real error.
        }

        return new BlockDeviceInfo(path, name, size, null, false);
    }

    /// <summary>
    /// Maps a path such as <c>/dev/sda3</c>, <c>/dev/disk/by-id/...</c> or <c>/dev/nvme0n1p2</c>
    /// onto its kernel device name.
    /// </summary>
    private static string ResolveDeviceName(string path) {
        string resolved = path;

        try {
            FileSystemInfo? target = File.ResolveLinkTarget(path, true);
            if (target != null) {
                resolved = target.FullName;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            // Use the path as given.
        }

        return Path.GetFileName(resolved);
    }

    private static bool IsIgnoredName(string name) {
        foreach (string prefix in IgnoredPrefixes) {
            if (name.StartsWith(prefix, StringComparison.Ordinal)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Every whole disk that backs something the host has mounted or is using as swap. This is how
    /// the machine avoids handing the guest the disk it booted off, without needing to know how it
    /// booted.
    /// </summary>
    private static HashSet<string> GetDisksInUseByHost() {
        HashSet<string> disks = [];

        foreach (string line in ReadLinesSafe("/proc/mounts")) {
            string[] parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) {
                continue;
            }

            AddHoldingDisks(disks, parts[0]);
        }

        foreach (string line in ReadLinesSafe("/proc/swaps").Skip(1)) {
            string[] parts = line.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) {
                continue;
            }

            AddHoldingDisks(disks, parts[0]);
        }

        return disks;
    }

    private static void AddHoldingDisks(HashSet<string> disks, string devicePath) {
        if (!devicePath.StartsWith("/dev/", StringComparison.Ordinal)) {
            return;
        }

        AddHoldingDisksByName(disks, ResolveDeviceName(devicePath), 0);
    }

    /// <summary>
    /// Walks from a block device name up to the whole disk (or disks) it lives on: partitions map to
    /// their parent, and device-mapper/raid targets map to each of their slaves.
    /// </summary>
    private static void AddHoldingDisksByName(HashSet<string> disks, string name, int depth) {
        if (name.Length == 0 || depth > 8 || !disks.Add(name)) {
            return;
        }

        string directory = Path.Combine(SysClassBlock, name);
        if (!Directory.Exists(directory)) {
            return;
        }

        // Stacked devices (LVM, mdraid, crypt) sit on top of other block devices.
        string slaves = Path.Combine(directory, "slaves");
        if (Directory.Exists(slaves)) {
            foreach (string slave in EnumerateEntriesSafe(slaves)) {
                AddHoldingDisksByName(disks, Path.GetFileName(slave), depth + 1);
            }
        }

        // A partition lives in a directory named after its disk.
        if (!File.Exists(Path.Combine(directory, "partition"))) {
            return;
        }

        try {
            FileSystemInfo? real = Directory.ResolveLinkTarget(directory, true);
            string? parent = Path.GetDirectoryName(real?.FullName);
            string parentName = Path.GetFileName(parent ?? "");

            if (parentName.Length > 0 && parentName != "block") {
                AddHoldingDisksByName(disks, parentName, depth + 1);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            // Not fatal, we just cannot tell what disk this partition belongs to.
        }
    }

    /// <summary>
    /// Reads a sysfs <c>size</c> file, which is always in 512 byte sectors regardless of the
    /// device's own block size.
    /// </summary>
    private static long ReadSize(string path) {
        return long.TryParse(ReadTextSafe(path), out long sectors) ? sectors * SectorSize : 0;
    }

    private static bool ReadFlag(string path) {
        return ReadTextSafe(path) == "1";
    }

    private static string? ReadModel(string directory) {
        string? model = ReadTextSafe(Path.Combine(directory, "device", "model"));
        if (string.IsNullOrWhiteSpace(model)) {
            model = ReadTextSafe(Path.Combine(directory, "device", "name"));
        }

        return string.IsNullOrWhiteSpace(model) ? null : model;
    }

    private static string? ReadTextSafe(string path) {
        try {
            return File.ReadAllText(path).Trim();
        }
        catch (Exception) {
            // sysfs entries appear and disappear, and some are not readable at all.
            return null;
        }
    }

    private static string[] ReadLinesSafe(string path) {
        try {
            return File.ReadAllLines(path);
        }
        catch (Exception) {
            return [];
        }
    }

    private static IEnumerable<string> EnumerateEntriesSafe(string path) {
        try {
            return Directory.GetFileSystemEntries(path);
        }
        catch (Exception) {
            return [];
        }
    }
}
