using CatData;
using CatVM;
using CatVM.DebugAdapter;
using CatVM.Debugger;

namespace CatLauncher;

/// <summary>
/// Builds a VM for the debug adapter by translating the launch request into the same argument
/// vector <c>catlaunch run</c> takes, so hardware discovery and device parsing are shared.
/// </summary>
internal sealed class LauncherVmFactory : IVmLauncher {
    public LaunchedVm Launch(LaunchConfig config) {
        if (config.Cwd != null) {
            if (!Directory.Exists(config.Cwd)) {
                throw new ArgumentException($"cwd does not exist: {config.Cwd}");
            }

            Directory.SetCurrentDirectory(config.Cwd);
        }

        string program = Path.GetFullPath(config.Program);
        if (!File.Exists(program)) {
            throw new ArgumentException($"ROM not found: {program}");
        }

        List<string> args = ["--rom", program];
        if (config.Memory.HasValue) args.AddRange(["--memory", config.Memory.Value.ToString()]);
        if (config.Ops.HasValue) args.AddRange(["--ops", config.Ops.Value.ToString()]);
        if (config.Fast) args.Add("--fast");
        if (config.TestInts) args.Add("--test-ints");
        if (config.DumpErrors) args.Add("--dump-errors");
        if (config.DisableHardwareManager) args.Add("--disable-hardware-manager");
        foreach (DeviceSpec device in config.Devices) {
            args.AddRange(["-d", device.Name]);
            if (!string.IsNullOrWhiteSpace(device.Arguments)) args.Add(device.Arguments);
        }
        args.AddRange(config.ExtraArgs);

        (CatVm vm, List<object> devices, CancellationTokenSource cts, _) = Program.SetupVm(args);
        vm.Paused = true;

        string debugTablePath = Path.GetFullPath(config.DebugTable ?? program + ".debug");
        bool hasSymbols;
        DebugTable table;
        try {
            hasSymbols = CatDebugger.TryLoadDebugTableFile(debugTablePath, out table);
        }
        catch (Exception e) {
            throw new ArgumentException($"Could not read debug symbols from {debugTablePath}: {e.Message}");
        }

        return new LaunchedVm(vm, table, hasSymbols, debugTablePath, () => Program.CleanVm(cts, devices));
    }
}
