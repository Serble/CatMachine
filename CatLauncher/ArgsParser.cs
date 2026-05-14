using System.Reflection;
using CatLauncher.Args;

namespace CatLauncher;

public static class ArgsParser {
    public static T Parse<T>(IEnumerable<string> argsArray) where T : new() {
        return Parse(new T(), argsArray);
    }
    
    public static T Parse<T>(T argContainer, IEnumerable<string> argsArray) {
        Dictionary<string, Argument> arguments = [];
        foreach (FieldInfo field in typeof(T).GetFields()
                     .Where(f => f.FieldType.IsAssignableTo(typeof(Argument)))) {
            Argument argument = (Argument)field.GetValue(argContainer)!;
            foreach (string arg in argument.Names) {
                arguments.Add(arg, argument);
            }
        }
        
        using IEnumerator<string> args = argsArray.GetEnumerator();
        
        while (args.MoveNext()) {
            string arg = args.Current;

            if (!arg.StartsWith('-')) {
                throw new ArgumentException("Arguments must start with - or --");
            }

            string[] argArgs = [];
            if (arg.StartsWith("--")) {
                if (arg.Length == 3) {
                    throw new ArgumentException("Unknown argument " + arg);
                }
                
                argArgs = [arg[2..]];
            }
            else if (arg.StartsWith('-')) {
                argArgs = arg.Skip(1).Select(c => c.ToString()).ToArray();
            }

            string? nextInvalid = null;
            foreach (string argArg in argArgs) {
                if (nextInvalid != null) {
                    throw new ArgumentException($"You cannot chain after the {nextInvalid} argument");
                }
                
                if (!arguments.TryGetValue(argArg, out Argument? argument)) {
                    throw new ArgumentException("Unknown argument: " + (argArg.Length == 1 ? '-' : "--") + argArg);
                }
                
                argument.DoParse(argArg, args);
                nextInvalid = argument.Chainable ? null : argArg;
            }
        }

        return argContainer;
    }
}

public class Arguments {
    public Dictionary<string, SerialDeviceArgument> DeviceArgs { get; }

    public readonly RomArgument Rom = new("rom", "r");
    public readonly FlagArgument Fast = new("fast", "f");
    public readonly IntArgument Ops = new(["ops", "o"], 100_000);
    public readonly IntArgument Memory = new(["memory", "m"], 1024 * 1024 * 16, 1, int.MaxValue);
    public readonly FlagArgument TestInts = new("test-ints");
    public readonly FlagArgument DumpErrors = new("dump-errors");
    public readonly DevicesArgument Devices;

#if DEBUG
    public readonly FlagArgument ProtectRom = new("protect-rom");
    public readonly MemDisallowArgument DisallowWrites = new("disallow-write");
    public readonly MemDisallowArgument DisallowReads = new("disallow-read");
#endif

    public Arguments(Dictionary<string, SerialDeviceArgument> deviceArgs) {
        Devices = new DevicesArgument(this, "device", "d");
        DeviceArgs = deviceArgs;
    }
}
