namespace CatLauncher.Args;

public class IntArgument(string[] names, long? defaultValue = null, long? minimum = null, long? maximum = null) : Argument(false, false, names) {
    public long? Value { get; private set; } = defaultValue;
    
    public override void Parse(string name, ArgIterator args) {
        if (!args.Next(out string? resultStr) || !long.TryParse(resultStr, out long result)) {
            throw new ArgumentException($"Must have integer value for {Names[0]} argument");
        }

        // Null long comparisons always return false
        if (result < minimum || result >= maximum) {
            throw new ArgumentException($"{Names[0]} must be in the range {minimum} and {maximum - 1}");
        }

        Value = result;
    }

    public static implicit operator long?(IntArgument value) {
        return value.Value;
    }
}
