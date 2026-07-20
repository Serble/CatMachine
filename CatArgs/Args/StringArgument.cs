namespace CatArgs.Args;

public class StringArgument(string[] names, string? defaultValue = null, bool positional = false) : Argument(defaultValue == null, false, false, positional, names) {
    public string? Value { get; private set; } = defaultValue;
    
    public override void Parse(string? name, ArgIterator args) {
        if (!args.Next(out string? result)) {
            throw new ArgumentException("You must supply a value for this argument");
        }

        Value = result;
    }

    public static implicit operator string?(StringArgument value) {
        return value.Value;
    }
}
