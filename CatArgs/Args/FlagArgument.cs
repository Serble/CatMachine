namespace CatArgs.Args;

public class FlagArgument(params string[] names) : Argument(false, true, false, false, names) {
    public bool Enabled;
    
    public override void Parse(string? name, ArgIterator args) {
        Enabled = true;
    }
    
    public static implicit operator bool(FlagArgument value) {
        return value.Enabled;
    }
}
