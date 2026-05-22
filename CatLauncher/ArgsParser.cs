using System.Reflection;
using CatLauncher.Args;

namespace CatLauncher;

public static class ArgsParser {
    public static T Parse<T>(T argContainer, IEnumerable<string> argsArray) {
        Dictionary<string, Argument> arguments = [];
        List<Argument> argumentsNoDupes = [];
        foreach (FieldInfo field in typeof(T).GetFields()
                     .Where(f => f.FieldType.IsAssignableTo(typeof(Argument)))) {
            Argument argument = (Argument)field.GetValue(argContainer)!;
            argumentsNoDupes.Add(argument);
            foreach (string arg in argument.Names) {
                arguments.Add(arg, argument);
            }
        }
        
        ArgIterator args = new(argsArray.ToArray());
        
        while (args.Next(out string? arg)) {
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
        
        List<Argument> neededArgs = argumentsNoDupes.Where(a => a.Required && !a.HasParsed).ToList();
        if (neededArgs.Count != 0) {
            throw new ArgumentException($"Missing required arguments: {string.Join(", ", neededArgs.Select(a => a.Names[0]))}");
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
    public readonly FlagArgument DisableHardwareManager = new("disable-hardware-manager");
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
