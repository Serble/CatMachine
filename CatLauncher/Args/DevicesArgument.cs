using System.Reflection;
using CatVM;

namespace CatLauncher.Args;

public class DevicesArgument(Arguments argContainer, params string[] names) : Argument(false, false, true, names) {
    public List<(SerialDeviceArgument, Dictionary<string, object?>)> DevicesToAdd { get; } = [];
    
    public override void Parse(string name, ArgIterator args) {
        if (!args.Next(out string? deviceName)) {
            throw new ArgumentException("device argument must take arguments");
        }
        
        if (!argContainer.DeviceArgs.TryGetValue(deviceName, out SerialDeviceArgument? deviceArgument)) {
            throw new ArgumentException($"Device {deviceName} does not exist!");
        }

        Dictionary<string, object?> parameters = [];
        
        // does it have a second argument?
        if (!args.Peek(out string? deviceArgsStr) || deviceArgsStr.StartsWith('-')) {
            // it does not, does this device require any arguments
            if (deviceArgument.Arguments.Values.Any(a =>
                    a.Type is not (
                        SerialDeviceArgument.ArgumentType.CatVm or
                        SerialDeviceArgument.ArgumentType.CancellationToken
                    ) && !a.HasDefault)) {
                throw new ArgumentException($"Device {deviceName} requires arguments");
            }
            
            // it doesn't requre args, we add it with no arguments
            DevicesToAdd.Add((deviceArgument, parameters));
            return;
        }

        // we actually want to consume the argument, because it is valid
        args.Next(out _);

        string[] deviceArgs = deviceArgsStr.Split(',');
        foreach (string deviceArg in deviceArgs) {
            string[] kv = deviceArg.Split(':', 2);
            if (kv.Length != 2) {
                throw new ArgumentException($"Each device argument must be key:value and be separated by commas ({deviceName})");
            }
            
            if (!deviceArgument.Arguments.TryGetValue(kv[0], out SerialDeviceArgument.Argument? def)) {
                // if there is no port argument and this is a directly registerable type then we will add the port
                // argument ourselves.
                if (kv[0] != "port" || !deviceArgument.Register) {
                    throw new ArgumentException($"Unknown argument for {deviceName}: {kv[0]}");
                }
                
                // create port argument
                def = new SerialDeviceArgument.Argument(false, null,
                    SerialDeviceArgument.ArgumentType.UInt);
            }

            object? value = GetValue(kv, def, deviceName);
            
            if (!parameters.TryAdd(kv[0], value)) {
                throw new ArgumentException($"You cannot set the same argument twice on a device ({deviceName})");
            }
        }
        
        DevicesToAdd.Add((deviceArgument, parameters));
    }

    private object? GetValue(string[] kv, SerialDeviceArgument.Argument def, string deviceName) {
        SerialDeviceArgument.ArgumentType type = def.Type;
        // if the type is nullable
        if ((int)type >= (int)SerialDeviceArgument.ArgumentType.NullableSByte &&
            (int)type < (int)SerialDeviceArgument.ArgumentType.CatVm) {
            if (kv[1].Equals("null", StringComparison.CurrentCultureIgnoreCase)) {
                return null;
            }
            
            type = (SerialDeviceArgument.ArgumentType)((int)type - 11);
        }
        
        // the nullable types are handles above with the - 11
        // and CatVm and CancellationToken are not settable, so throw UnknownArgument
        return type switch {
            SerialDeviceArgument.ArgumentType.String => kv[1],
            SerialDeviceArgument.ArgumentType.SByte => sbyte.TryParse(kv[1], out sbyte result) ? result :
                throw new ArgumentException($"Invalid argument for {deviceName}: {kv[0]}, must be a signed byte"),
            SerialDeviceArgument.ArgumentType.Byte => byte.TryParse(kv[1], out byte result) ? result :
                throw new ArgumentException($"Invalid argument for {deviceName}: {kv[0]}, must be a byte"),
            SerialDeviceArgument.ArgumentType.UShort => ushort.TryParse(kv[1], out ushort result) ? result
                : throw new ArgumentException($"Invalid argument for {deviceName}: {kv[0]}, must be an unsigned short"),
            SerialDeviceArgument.ArgumentType.Short => short.TryParse(kv[1], out short result) ? result
                : throw new ArgumentException($"Invalid argument for {deviceName}: {kv[0]}, must be a short"),
            SerialDeviceArgument.ArgumentType.UInt => uint.TryParse(kv[1], out uint result) ? result
                : throw new ArgumentException($"Invalid argument for {deviceName}: {kv[0]}, must be an unsigned int"),
            SerialDeviceArgument.ArgumentType.Int => int.TryParse(kv[1], out int result) ? result :
                throw new ArgumentException($"Invalid argument for {deviceName}: {kv[0]}, must be an int"),
            SerialDeviceArgument.ArgumentType.ULong => ulong.TryParse(kv[1], out ulong result) ? result :
                throw new ArgumentException($"Invalid argument for {deviceName}: {kv[0]}, must be an unsigned long"),
            SerialDeviceArgument.ArgumentType.Long => long.TryParse(kv[1], out long result) ? result :
                throw new ArgumentException($"Invalid argument for {deviceName}: {kv[0]}, must be a long"),
            SerialDeviceArgument.ArgumentType.Float => float.TryParse(kv[1], out float result) ? result :
                throw new ArgumentException($"Invalid argument for {deviceName}: {kv[0]}, must be a float"),
            SerialDeviceArgument.ArgumentType.Double => double.TryParse(kv[1], out double result) ? result :
                throw new ArgumentException($"Invalid argument for {deviceName}: {kv[0]}, must be a double"),
            SerialDeviceArgument.ArgumentType.Decimal => decimal.TryParse(kv[1], out decimal result) ? result :
                throw new ArgumentException($"Invalid argument for {deviceName}: {kv[0]}, must be a decimal"),
            
            // CatVm and CancellationToken cannot be changed by the user
            _ => throw new ArgumentException($"Unknown argument for {deviceName}: {kv[0]}")
        };
    }
}
