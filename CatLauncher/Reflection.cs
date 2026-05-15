using System.Data;
using System.Reflection;
using CatData;
using CatVM;

namespace CatLauncher;

public static class Reflection {
    public static Dictionary<string, SerialDeviceArgument> GetSerialDevices(Assembly assembly) {
        List<Type> validParamTypes = [
            typeof(string), typeof(byte), typeof(sbyte), typeof(ushort), typeof(short), typeof(uint), typeof(int),
            typeof(ulong), typeof(long), typeof(float), typeof(double), typeof(decimal),
            typeof(byte?), typeof(sbyte?), typeof(ushort?), typeof(short?), typeof(uint?), typeof(int?),
            typeof(ulong?), typeof(long?), typeof(float?), typeof(double?), typeof(decimal?)
        ];
        
        IEnumerable<(ConstructorInfo, CommandLineConstructableAttribute)> constructors = assembly.GetTypes()
            .SelectMany(type => type.GetConstructors()
                .Select(x => (x, Att: x.GetCustomAttribute<CommandLineConstructableAttribute>()))
                .Where(x => x.Att != null))!;

        Dictionary<string, SerialDeviceArgument> arguments = [];
        
        foreach ((ConstructorInfo constructor, CommandLineConstructableAttribute attribute) in constructors) {
            SerialDeviceArgument arg = new(attribute.Name, attribute.Register, attribute.PortValues, constructor);
            
            foreach (ParameterInfo parameter in constructor.GetParameters()) {
                int typeIndex = validParamTypes.IndexOf(parameter.ParameterType);
                if (typeIndex == -1) {
                    if (parameter.ParameterType == typeof(CatVm)) {
                        arg.Arguments.Add(parameter.Name!, new SerialDeviceArgument.Argument(false,
                            null, SerialDeviceArgument.ArgumentType.CatVm));
                    }
                    else if (parameter.ParameterType == typeof(CancellationToken)) {
                        arg.Arguments.Add(parameter.Name!, new SerialDeviceArgument.Argument(false,
                            null, SerialDeviceArgument.ArgumentType.CancellationToken));
                    }
                    else {
                        throw new ArgumentException("invalid argument type in constructor");                        
                    }
                    
                    continue;
                }

                // if (typeIndex > 11) {
                //     typeIndex -= 11;
                // }
                
                
                arg.Arguments.Add(parameter.Name!, new SerialDeviceArgument.Argument(
                    parameter.HasDefaultValue, parameter.DefaultValue, (SerialDeviceArgument.ArgumentType)typeIndex));
            }

            if (!arguments.TryAdd(arg.Name, arg)) {
                throw new DuplicateNameException($"Multiple serial devices have the name: {arg.Name}");
            }
        }

        return arguments;
    }
}
