using System.Reflection;
using CatVM;

namespace CatLauncher.Args;

public class DevicesArgument(Arguments argContainer, params string[] names) : Argument(false, true, names) {
    public List<(SerialDeviceArgument, Dictionary<string, object>)> DevicesToAdd { get; } = [];
    
    public override void Parse(string name, IEnumerator<string> args) {
        if (!args.MoveNext()) {
            throw new ArgumentException("device argument must take arguments");
        }

        string deviceName = args.Current;
        if (!argContainer.DeviceArgs.TryGetValue(deviceName, out SerialDeviceArgument? deviceArgument)) {
            throw new ArgumentException($"Device {deviceName} does not exist!");
        }

        Dictionary<string, object> parameters = [];
        if (!deviceArgument.Arguments.Values.Any(a =>
                a.Type is not (SerialDeviceArgument.ArgumentType.CatVm or SerialDeviceArgument.ArgumentType.CancellationToken))) {
            DevicesToAdd.Add((deviceArgument, parameters));
            return;
        }
        
        if (!args.MoveNext()) {
            throw new ArgumentException($"Device {deviceName} requires arguments");
        }

        string[] deviceArgs = args.Current.Split(',');
        foreach (string deviceArg in deviceArgs) {
            string[] kv = deviceArg.Split(',', 2);
            if (kv.Length != 2) {
                throw new ArgumentException($"Each device argument must be key:value and be separated by commas ({deviceName})");
            }
            
            if (!deviceArgument.Arguments.TryGetValue(kv[0], out SerialDeviceArgument.Argument? def)) {
                throw new ArgumentException($"Unknown argument for {deviceName}: {kv[0]}");
            }

            object value = def.Type switch {
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
            
            if (!parameters.TryAdd(kv[0], value)) {
                throw new ArgumentException($"You cannot set the same argument twice on a device ({deviceName})");
            }
        }
        
        DevicesToAdd.Add((deviceArgument, parameters));
    }
}
