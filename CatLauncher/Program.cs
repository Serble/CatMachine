using System.Reflection;
using CatVM;
using CatVM.Debugging;
using CatVM.Extensions;

namespace CatLauncher;

static class Program {
    public static async Task<int> Main(string[] args) {
        Console.WriteLine(string.Join(' ', args));
        if (args.Length == 0) {
            string command = Environment.CommandLine.Split(' ', 2)[0];
            Console.WriteLine($"{command} run <args>\n" +
                              $"{command} debug <args>");
            return 1;
        }

        string operation = args[0];

        switch (operation) {
            case "run": {
                (CatVm vm, List<object> devices, CancellationTokenSource cts, _) = SetupVm(args.Skip(1));
                vm.Run(cts.Token);
                await CleanVm(vm, cts, devices);
                return 0;
            }
            
            case "debug": {
                (CatVm vm, List<object> devices, CancellationTokenSource cts, Arguments result) = SetupVm(args.Skip(1));
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

    private static (CatVm vm, List<object> devices, CancellationTokenSource cts, Arguments args) SetupVm(IEnumerable<string> args) {
        // TODO: Required args
        Dictionary<string, SerialDeviceArgument> deviceInfos =
            Reflection.GetSerialDevices(Assembly.GetAssembly(typeof(CatVm))!);
        Arguments result = ArgsParser.Parse(new Arguments(deviceInfos), args);
        
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
        
        vm.RegisterSerialDevice(0, new HardwareManager());

        foreach ((SerialDeviceArgument deviceDef, Dictionary<string, object> parameters) in
                 result.Devices.DevicesToAdd) {
            List<object> constructorArgs = [];

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

                        if (argument.DefaultValue == null) {
                            throw new ArgumentException($"Missing required argument {key} for device {deviceDef.Name}");
                        }

                        constructorArgs.Add(argument.DefaultValue);
                        break;
                }
            }

            devices.Add(deviceDef.Constructor.Invoke(constructorArgs.ToArray()));
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
