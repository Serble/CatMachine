using System.Globalization;

namespace CatVM.Metal;

/// <summary>
/// Machine configuration. This is deliberately a hand written parser rather than reusing CatArgs +
/// reflection like the reference launcher does, because Metal must stay Native AOT compatible.
/// </summary>
public sealed class MetalOptions {
    /// <summary>Path of the firmware ROM the machine boots from, if one was given explicitly.</summary>
    public string? FirmwarePath { get; private set; }

    /// <summary>Amount of RAM in bytes.</summary>
    public int Memory { get; private set; } = 16 * 1024 * 1024;

    /// <summary>
    /// The speed the CPU is paced to. Only meaningful when <see cref="Fast"/> is off: an uncapped VM
    /// ignores the cycle rate entirely and measures time off the host clock instead.
    /// </summary>
    public uint Ops { get; private set; } = 10_000_000;

    /// <summary>Whether a cycle rate was asked for, so that ignoring it can be reported.</summary>
    public bool OpsSpecified { get; private set; }

    /// <summary>
    /// Run the CPU as fast as the host allows instead of pacing it to <see cref="Ops"/>. On by
    /// default: a physical machine should use the hardware it has rather than pretend to be slower.
    /// </summary>
    public bool Fast { get; private set; } = true;

    /// <summary>Enable the debug interrupts (<c>0x90</c>), useful when bringing up firmware.</summary>
    public bool TestInterrupts { get; private set; }

    /// <summary>Print host stack traces for guest faults.</summary>
    public bool DumpErrors { get; private set; }

    /// <summary>Block devices given explicitly with <c>--disk</c>. These bypass all safety checks.</summary>
    public List<string> Disks { get; } = [];

    /// <summary>Block devices to leave out of automatic discovery.</summary>
    public List<string> ExcludedDisks { get; } = [];

    /// <summary>Whether to attach every safe block device found in /sys/block.</summary>
    public bool AutoDetectDisks { get; private set; } = true;

    /// <summary>Simulated seek/transfer cost per 512 byte block. 0 means "as fast as the host".</summary>
    public long DiskPicosPerBlock { get; private set; }

    /// <summary>Open disks with O_SYNC so guest writes survive the power being cut.</summary>
    public bool SyncWrites { get; private set; } = true;

    /// <summary>Take over the whole screen and hide the host cursor.</summary>
    public bool Fullscreen { get; private set; } = true;

    /// <summary>Draw the host frame rate over the guest display.</summary>
    public bool ShowFps { get; private set; }

    /// <summary>Whether a guest shutdown request powers the physical machine off.</summary>
    public bool PowerControl { get; private set; } = true;

    /// <summary>Print the device list and exit without starting the CPU.</summary>
    public bool ListDevices { get; private set; }

    public bool ShowHelp { get; private set; }

    public static bool TryParse(IReadOnlyList<string> args, out MetalOptions options, out string? error) {
        MetalOptions result = new();
        options = result;
        error = null;

        bool pacingChosen = false;
        bool opsGiven = false;

        for (int i = 0; i < args.Count; i++) {
            string arg = args[i];

            switch (arg) {
                case "-h" or "--help":
                    result.ShowHelp = true;
                    return true;

                case "--list-devices":
                    result.ListDevices = true;
                    break;

                case "-F" or "--firmware":
                    if (!TryNext(args, ref i, arg, out string? firmware, out error)) {
                        return false;
                    }
                    result.FirmwarePath = firmware;
                    break;

                case "-m" or "--memory":
                    if (!TryNext(args, ref i, arg, out string? memory, out error)) {
                        return false;
                    }
                    if (!TryParseSize(memory, 1024, out long memoryBytes) ||
                        memoryBytes is < 1024 or > int.MaxValue) {
                        error = $"Invalid value for {arg}: {memory}";
                        return false;
                    }
                    result.Memory = (int)memoryBytes;
                    break;

                case "-o" or "--ops":
                    if (!TryNext(args, ref i, arg, out string? ops, out error)) {
                        return false;
                    }
                    if (!TryParseSize(ops, 1000, out long opsValue) || opsValue is < 1 or > uint.MaxValue) {
                        error = $"Invalid value for {arg}: {ops}";
                        return false;
                    }
                    result.Ops = (uint)opsValue;
                    result.OpsSpecified = true;
                    opsGiven = true;
                    break;

                case "-f" or "--fast":
                    result.Fast = true;
                    pacingChosen = true;
                    break;

                case "--no-fast":
                    result.Fast = false;
                    pacingChosen = true;
                    break;

                case "--test-ints":
                    result.TestInterrupts = true;
                    break;

                case "--dump-errors":
                    result.DumpErrors = true;
                    break;

                case "-d" or "--disk":
                    if (!TryNext(args, ref i, arg, out string? disk, out error)) {
                        return false;
                    }
                    result.Disks.Add(disk!);
                    break;

                case "--exclude-disk":
                    if (!TryNext(args, ref i, arg, out string? excluded, out error)) {
                        return false;
                    }
                    result.ExcludedDisks.Add(excluded!);
                    break;

                case "--no-auto-disks":
                    result.AutoDetectDisks = false;
                    break;

                case "--disk-picos-per-block":
                    if (!TryNext(args, ref i, arg, out string? picos, out error)) {
                        return false;
                    }
                    if (!TryParseSize(picos, 1000, out long picosValue) || picosValue < 0) {
                        error = $"Invalid value for {arg}: {picos}";
                        return false;
                    }
                    result.DiskPicosPerBlock = picosValue;
                    break;

                case "--no-sync-writes":
                    result.SyncWrites = false;
                    break;

                case "--no-fullscreen":
                    result.Fullscreen = false;
                    break;

                case "--fps":
                    result.ShowFps = true;
                    break;

                case "--no-power-control":
                    result.PowerControl = false;
                    break;

                default:
                    error = $"Unknown option: {arg}";
                    return false;
            }
        }

        // A cycle rate only means anything if the CPU is paced to it: an uncapped VM ignores it and
        // takes its timings from the host clock. So --ops turns pacing on unless --fast was asked for
        // as well.
        if (opsGiven && !pacingChosen) {
            result.Fast = false;
        }

        return true;
    }

    public static void PrintUsage() {
        Console.WriteLine(
            """
            CatVM.Metal - runs a Cat machine directly on real hardware.

            Usage: CatVM.Metal [options]

            Firmware:
              -F, --firmware PATH        ROM to boot from. Defaults to $CATVM_FIRMWARE, then
                                         /etc/catvm/firmware.rom, /opt/catvm/firmware.rom, then
                                         firmware.rom next to this binary.

            CPU and memory:
              -m, --memory SIZE          RAM size, K/M/G are powers of 1024 (default 16M)
              -f, --fast                 Run flat out, ignoring cycle timings. This is the default.
                                         Uptime and hardware timers then run on host time.
              -o, --ops COUNT            Cycles per second, K/M/G are powers of 1000 (default 10M).
                                         Giving this paces the CPU to that speed. It does nothing
                                         while the CPU is uncapped, where the cycle rate is ignored.
                  --no-fast              Pace the CPU to --ops
                  --test-ints            Enable the debug interrupts (0x90)
                  --dump-errors          Print host exceptions behind guest faults

            Disks:
              -d, --disk PATH            Attach a specific block device or image file, before any
                                         discovered ones. May be repeated. No safety check is applied
                                         to a path given this way.
                  --exclude-disk PATH    Leave a device out of automatic discovery
                  --no-auto-disks        Do not attach automatically discovered devices
                  --disk-picos-per-block N   Simulated cost per 512 byte block (default 0)
                  --no-sync-writes       Do not open disks with O_SYNC (faster, less durable)

            Display:
                  --no-fullscreen        Run in a window instead of taking over the screen
                  --fps                  Draw the host frame rate over the guest display

            Machine:
                  --no-power-control     Do not power the host off when the guest shuts down
                  --list-devices         Print the hardware that would be attached, then exit
              -h, --help                 Show this help
            """);
    }

    private static bool TryNext(IReadOnlyList<string> args, ref int i, string name, out string? value,
        out string? error) {
        if (i + 1 >= args.Count) {
            value = null;
            error = $"{name} requires a value";
            return false;
        }

        value = args[++i];
        error = null;
        return true;
    }

    /// <summary>
    /// Parses a plain number or a number with a K/M/G suffix.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="unit">
    /// What a K is worth: 1024 for quantities of memory, 1000 for everything else, since a clock rate
    /// written as 50M means 50 million and nothing else.
    /// </param>
    /// <param name="value">The parsed value.</param>
    private static bool TryParseSize(string? text, long unit, out long value) {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) {
            return false;
        }

        text = text.Trim();
        long multiplier = 1;

        char suffix = char.ToLowerInvariant(text[^1]);
        switch (suffix) {
            case 'k':
                multiplier = unit;
                break;

            case 'm':
                multiplier = unit * unit;
                break;

            case 'g':
                multiplier = unit * unit * unit;
                break;
        }

        if (multiplier != 1) {
            text = text[..^1];
        }

        if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed)) {
            return false;
        }

        try {
            value = checked(parsed * multiplier);
        }
        catch (OverflowException) {
            return false;
        }

        return true;
    }
}
