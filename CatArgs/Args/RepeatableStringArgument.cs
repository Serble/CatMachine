namespace CatArgs.Args;

public class RepeatableStringArgument(string[] names, string[]? defaultValues = null, bool positional = false) : Argument(defaultValues == null, false, true, positional, names) {
    public string[]? Values { get; private set; } = defaultValues;
    
    public override void Parse(string? name, ArgIterator args) {
        if (!args.Next(out string? result)) {
            throw new ArgumentException("You must supply a value for this argument");
        }

        Values = Values == null ? [result] : Values.Append(result).ToArray();
    }

    public static implicit operator string[]?(RepeatableStringArgument value) {
        return value.Values;
    }
}
