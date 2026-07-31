using System.Runtime.InteropServices;
using CatVM.Metal.Hardware;
using DiskDevice;
using HardwareManagerDevice;
using HardwareTimerDevice;
using RaylibPpuDevice;

namespace CatVM.Metal;

/// <summary>
/// Entry point of the Metal machine: turns the host's real hardware into Cat serial devices, then
/// hands control to the firmware ROM.
/// </summary>
public static class Program {
    /// <summary>
    /// Where the firmware ROM is looked for when <c>--firmware</c> is not given, in order.
    /// </summary>
    private static readonly string[] FirmwareSearchPaths = [
        "/etc/catvm/firmware.rom",
        "/opt/catvm/firmware.rom"
    ];

    /// <summary>
    /// How long to let queued disk writes drain after the CPU stops, before the disks are closed.
    /// </summary>
    private static readonly TimeSpan DiskDrainGrace = TimeSpan.FromMilliseconds(500);

    private static readonly List<BlockDeviceStream> OpenDisks = [];

    public static int Main(string[] args) {
        if (!MetalOptions.TryParse(args, out MetalOptions options, out string? error)) {
            Log.Error(error!);
            Console.Error.WriteLine("Run with --help for usage.");
            return 1;
        }

        if (options.ShowHelp) {
            MetalOptions.PrintUsage();
            return 0;
        }

        Log.Info($"CatVM Metal on {RuntimeInformation.RuntimeIdentifier}");

        if (!options.AutoDetectDisks && options.Disks.Count == 0) {
            Log.Warn("--no-auto-disks was given with no --disk, so the machine will have no disks");
        }

        List<BlockDeviceInfo> disks = FindDisks(options);

        if (options.ListDevices) {
            PrintDeviceMap(options, disks);
            return 0;
        }

        byte[]? firmware = LoadFirmware(options.FirmwarePath);
        if (firmware == null) {
            return 1;
        }

        CatVm vm;
        try {
            vm = new CatVm(options.Memory, options.Ops, firmware) {
                Fast = options.Fast,
                EnableTestingInterrupts = options.TestInterrupts,
                DumpErrors = options.DumpErrors
            };
        }
        catch (Exception ex) {
            Log.Error($"could not create the machine: {ex.Message}");
            return 1;
        }

        Log.Info($"{options.Memory / (1024 * 1024)} MiB of memory, " +
                 (options.Fast ? "CPU uncapped" : $"CPU paced to {options.Ops} cycles/second"));

        if (options.Fast && options.OpsSpecified) {
            // An uncapped VM takes its timings from the host clock, so the rate is dead weight.
            Log.Warn("--ops is ignored while the CPU is uncapped, drop --fast to pace the CPU to it");
        }

        // The disks keep running until their queued writes have drained, which happens after the CPU
        // has already stopped, so they get a cancellation token of their own.
        CancellationTokenSource cts = new();
        CancellationTokenSource diskCts = new();

        PowerControl.Action powerAction = PowerControl.Action.None;

        vm.RegisterSerialDevice(Ports.HardwareManager, new HardwareManager());
        vm.RegisterSerialDevice(Ports.Timer, new HardwareTimer());
        AttachDisks(vm, disks, options, diskCts.Token);

        // The PPU registers itself on the ports it is given and starts its own render thread. It does
        // not create a window until the guest picks a display mode, so Fullscreen is safe to set here.
        _ = new RaylibPpu(vm, Ports.Graphics, Ports.Keyboard, Ports.Mouse) {
            Fullscreen = options.Fullscreen,
            DrawFps = options.ShowFps
        };

        // A guest shutdown is a real shutdown: there is nothing behind this process to return to.
        vm.OnShutdown += () => {
            Log.Info("guest requested shutdown");
            powerAction = PowerControl.Action.PowerOff;
        };

        // Also close the disks on Environment.Exit, which the PPU calls if its window goes away.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseDisks(diskCts);

        using PosixSignalRegistration term = PosixSignalRegistration.Create(PosixSignal.SIGTERM,
            context => Stop(context, cts, "SIGTERM"));
        using PosixSignalRegistration interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT,
            context => Stop(context, cts, "SIGINT"));

        Log.Info("starting the CPU");
        vm.Run(cts.Token);
        Log.Info("the CPU stopped");

        CloseDisks(diskCts);
        vm.ReleaseResources();

        if (options.PowerControl) {
            PowerControl.Perform(powerAction);
        }
        else if (powerAction != PowerControl.Action.None) {
            Log.Info($"power control is disabled, not performing: {powerAction}");
        }

        return 0;
    }

    private static void Stop(PosixSignalContext context, CancellationTokenSource cts, string signal) {
        Log.Info($"{signal} received, stopping the CPU");
        context.Cancel = true;
        cts.Cancel();
    }

    /// <summary>
    /// Works out which block devices the guest gets. Explicit <c>--disk</c> paths come first, in the
    /// order given, so their ports are predictable; discovered devices follow.
    /// </summary>
    private static List<BlockDeviceInfo> FindDisks(MetalOptions options) {
        List<BlockDeviceInfo> disks = [];
        HashSet<string> seen = [];

        foreach (string path in options.Disks) {
            BlockDeviceInfo device = BlockDevices.Describe(path);
            if (seen.Add(device.Path)) {
                disks.Add(device);
            }
        }

        if (!options.AutoDetectDisks) {
            return disks;
        }

        foreach (BlockDeviceInfo device in BlockDevices.Discover(options.ExcludedDisks.Concat(options.Disks))) {
            if (seen.Add(device.Path)) {
                disks.Add(device);
            }
        }

        return disks;
    }

    private static void AttachDisks(CatVm vm, List<BlockDeviceInfo> disks, MetalOptions options,
        CancellationToken token) {
        uint port = Ports.FirstDisk;

        foreach (BlockDeviceInfo device in disks) {
            BlockDeviceStream stream;
            try {
                stream = BlockDeviceStream.Open(device, options.SyncWrites);
            }
            catch (Exception ex) {
                Log.Warn($"could not open {device.Path}: {ex.Message}");
                continue;
            }

            lock (OpenDisks) {
                OpenDisks.Add(stream);
            }

            vm.RegisterSerialDevice(port, new Disk(stream, options.DiskPicosPerBlock, token: token));
            Log.Info($"disk on port {port}: {device.Describe()}");
            port++;
        }

        if (port == Ports.FirstDisk) {
            // Not necessarily a problem: a firmware that is the whole program does not need a disk.
            Log.Info("no disks attached");
        }
    }

    /// <summary>
    /// Gives the disk devices a moment to write out anything the guest queued before the CPU stopped,
    /// then closes them.
    /// </summary>
    private static void CloseDisks(CancellationTokenSource diskCts) {
        lock (OpenDisks) {
            if (OpenDisks.Count == 0) {
                return;
            }

            if (!diskCts.IsCancellationRequested) {
                Thread.Sleep(DiskDrainGrace);
                diskCts.Cancel();
            }

            foreach (BlockDeviceStream disk in OpenDisks) {
                try {
                    disk.Dispose();
                }
                catch (Exception ex) {
                    Log.Warn($"could not close a disk cleanly: {ex.Message}");
                }
            }

            OpenDisks.Clear();
        }
    }

    private static byte[]? LoadFirmware(string? explicitPath) {
        foreach (string path in FirmwareCandidates(explicitPath)) {
            if (!File.Exists(path)) {
                continue;
            }

            try {
                byte[] rom = File.ReadAllBytes(path);
                if (rom.Length == 0) {
                    Log.Error($"firmware {path} is empty");
                    return null;
                }

                Log.Info($"firmware: {path} ({rom.Length} bytes)");
                return rom;
            }
            catch (Exception ex) {
                Log.Error($"could not read firmware {path}: {ex.Message}");
                return null;
            }
        }

        Log.Error(explicitPath != null
            ? $"firmware {explicitPath} was not found"
            : "no firmware ROM was found, pass one with --firmware");
        return null;
    }

    private static IEnumerable<string> FirmwareCandidates(string? explicitPath) {
        if (explicitPath != null) {
            yield return explicitPath;
            yield break;
        }

        string? fromEnvironment = Environment.GetEnvironmentVariable("CATVM_FIRMWARE");
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) {
            yield return fromEnvironment;
        }

        foreach (string path in FirmwareSearchPaths) {
            yield return path;
        }

        yield return Path.Combine(AppContext.BaseDirectory, "firmware.rom");
    }

    private static void PrintDeviceMap(MetalOptions options, List<BlockDeviceInfo> disks) {
        Console.WriteLine("Serial devices:");
        Console.WriteLine($"  {Ports.HardwareManager,4}  hardware manager  0x296C4EF5");
        Console.WriteLine($"  {Ports.Graphics,4}  display           0xFF64BEF9");
        Console.WriteLine($"  {Ports.Keyboard,4}  keyboard          0x2EB3AD76");
        Console.WriteLine($"  {Ports.Mouse,4}  mouse             0x25A3E57D");
        Console.WriteLine($"  {Ports.Timer,4}  timer             0xB1F91A0C");

        uint port = Ports.FirstDisk;
        foreach (BlockDeviceInfo device in disks) {
            Console.WriteLine($"  {port,4}  disk              0x96818B9A  {device.Describe()}");
            port++;
        }

        if (port == Ports.FirstDisk) {
            Console.WriteLine("  (no disks)");
        }

        Console.WriteLine($"Memory: {options.Memory} bytes");
        Console.WriteLine(options.Fast
            ? "CPU: uncapped, cycle rate ignored"
            : $"CPU: paced to {options.Ops} cycles/second");
    }
}
