using CatArgs;
using CatVM;
using CatVM.Debugging;
using CatVM.Extensions;
using CatVM.Serial;

namespace CatLauncher;

static class Program {
    public static async Task<int> Main(string[] args) {
        if (args.Length == 0) {
            string command = Environment.CommandLine.Split(' ', 2)[0];
            Console.WriteLine($"{command} run <args>\n" +
                              $"{command} debug <args>");
            return 1;
        }

        string operation = args[0];

        switch (operation) {
            case "run": {
                CatVm vm; List<object> devices; CancellationTokenSource cts;
                try {
                    (vm, devices, cts, _) = SetupVm(args.Skip(1));
                }
                catch (ArgumentException ex) {
                    Console.WriteLine(ex.Message);
                    return 1;
                }
                
                vm.Run(cts.Token);
                await CleanVm(vm, cts, devices);
                return 0;
            }
            
            case "debug": {
                CatVm vm; List<object> devices; CancellationTokenSource cts; RunArguments result;
                try {
                    (vm, devices, cts, result) = SetupVm(args.Skip(1));
                }
                catch (ArgumentException ex) {
                    Console.WriteLine(ex.Message);
                    return 1;
                }
                
                Debugger debugger = new(vm, result.Rom.Path!);
                debugger.StartUserDebugging();
                await CleanVm(vm, cts, devices);
                return 0;
            }
            
            default:
                Console.WriteLine("Invalid mode, valid modes are: run, debug");
                return 1;
        }
    }

    private static (CatVm vm, List<object> devices, CancellationTokenSource cts, RunArguments args) SetupVm(IEnumerable<string> args) {
        Reflection.LoadAssemblies(Path.Join(Directory.GetCurrentDirectory(), "hardware"));
        Reflection.LoadAssemblies(Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "catmachine/hardware"));
        
        Dictionary<string, SerialDeviceArgument> deviceInfos = Reflection.GetSerialDevices();
        RunArguments result = ArgsParser.Parse(new RunArguments(deviceInfos), args);
        
        CatVm vm = new((int)result.Memory!, (uint)result.Ops!, result.Rom.Rom) {
            Fast = result.Fast,
            EnableTestingInterrupts = result.TestInts,
            DumpErrors = result.DumpErrors,
#if DEBUG
            ErrorOnRomWrite = result.ProtectRom,
            DisallowedWriteRegions = result.DisallowWrites.Regions.ToArray(),
            DisallowedReadRegions = result.DisallowReads.Regions.ToArray()
#endif
        };
        
        CancellationTokenSource cts = new();
        List<object> devices = [];

        // Now register the serial devices, we will do the devices with a chosen port first, then let the
        // other devices with any port choose what is left.
        if (!result.DisableHardwareManager) {
            vm.RegisterSerialDevice(0, new HardwareManager());
        }
        
        // get which ones have a chosen port
        List<(SerialDeviceArgument, Dictionary<string, object?>)> portSelectedDevices = [];
        List<(SerialDeviceArgument, Dictionary<string, object?>)> otherDevices = [];
        
        foreach ((SerialDeviceArgument deviceDef, Dictionary<string, object?> parameters) in
                 result.Devices.DevicesToAdd) {
            if (deviceDef.PortValues.Any(parameters.ContainsKey)) {
                portSelectedDevices.Add((deviceDef, parameters));
            }
            else {
                otherDevices.Add((deviceDef, parameters));
            }
        }
        
        // initialize and register the devices
        foreach ((SerialDeviceArgument deviceDef, Dictionary<string, object?> parameters) in
                 portSelectedDevices.Concat(otherDevices)) {
            List<object?> constructorArgs = [];

            foreach ((string key, SerialDeviceArgument.Argument argument) in deviceDef.Arguments) {
                switch (argument.Type) {
                    case SerialDeviceArgument.ArgumentType.CatVm:
                        constructorArgs.Add(vm);
                        break;

                    case SerialDeviceArgument.ArgumentType.CancellationToken:
                        constructorArgs.Add(cts.Token);
                        break;

                    default:
                        if (parameters.TryGetValue(key, out object? value)) {
                            constructorArgs.Add(value);
                            break;
                        }

                        if (!argument.HasDefault) {
                            throw new ArgumentException($"Missing required argument {key} for device {deviceDef.Name}");
                        }

                        constructorArgs.Add(argument.DefaultValue);
                        break;
                }
            }

            object device = deviceDef.Constructor.Invoke(constructorArgs.ToArray());
            devices.Add(device);
            if (!deviceDef.Register) {
                continue;
            }
            
            if (device is not ISerialDevice serial) {
                throw new ArgumentException($"Registerable device {deviceDef.Name} is not an ISerialDevice");
            }

            if (parameters.TryGetValue("port", out object? port)) {
                vm.RegisterSerialDevice((uint)port!, serial);
            }
            else {
                vm.RegisterSerialDevice(serial);
            }
        }

        return (vm, devices, cts, result);
    }
    
    private static async Task CleanVm(CatVm vm, CancellationTokenSource cts, List<object> devices) {
        if (!cts.IsCancellationRequested) {
            await cts.CancelAsync();
        }
        
        foreach (object device in devices) {
            switch (device) {
                case IAsyncDisposable asyncDisposable:
                    await asyncDisposable.DisposeAsync();
                    break;
                
                case IDisposable disposable:
                    disposable.Dispose();
                    break;
            }
        }
    }
}
