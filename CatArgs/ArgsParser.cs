using System.Reflection;

namespace CatArgs;

public static class ArgsParser {
    /// <summary>
    /// Parses and returns arguments
    /// </summary>
    /// <param name="argContainer">An instance of T which defines what arguments exist and is what is used to access the arguments</param>
    /// <param name="argsArray">The arguments</param>
    /// <typeparam name="T">The type which defines what arguments exist</typeparam>
    /// <returns>The parsed arguments in the form of T</returns>
    /// <exception cref="InvalidOperationException">If the T class is formatted incorrectly</exception>
    /// <exception cref="ArgumentException">If an invalid argument is parsed (you probably want to catch and print this)</exception>
    public static T Parse<T>(T argContainer, IEnumerable<string> argsArray) {
        Dictionary<string, Argument> arguments = []; // name to argument (rom -> RomArgument)
        List<Argument> argumentsNoDupes = [];        // list of all arguments
        Argument? positionalArgument = null;         // the positional argument
        foreach (FieldInfo field in typeof(T).GetFields()
                     .Where(f => f.FieldType.IsAssignableTo(typeof(Argument)))) {
            Argument argument = (Argument)field.GetValue(argContainer)!;
            argumentsNoDupes.Add(argument);
            
            if (argument.Positional) {
                if (positionalArgument != null) {
                    throw new InvalidOperationException("There can only be one positional argument");
                }
                
                positionalArgument = argument;
            }
            
            foreach (string arg in argument.Names) {
                arguments.Add(arg, argument);
            }
        }
        
        ArgIterator args = new(argsArray.ToArray());
        
        // we use peek because we don't want to consume the argument if it is a positional argument
        while (args.Peek(out string? arg)) {
            if (!arg.StartsWith('-')) {
                if (positionalArgument == null) {
                    throw new ArgumentException("Arguments must start with - or --");
                }

                positionalArgument.DoParse(null, args);
                continue;
            }

            args.Next(out _); // this is the name, like --rom, we will consume the argument
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
        
        // check if all arguments that are required are being used and collect them if not being used
        List<Argument> neededArgs = argumentsNoDupes.Where(a => a.Required && !a.HasParsed).ToList();
        if (neededArgs.Count != 0) {
            throw new ArgumentException($"Missing required arguments: {string.Join(", ", neededArgs.Select(a => a.Names[0]))}");
        }
        
        return argContainer;
    }
}
