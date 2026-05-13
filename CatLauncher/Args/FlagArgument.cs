namespace CatLauncher.Args;

public class FlagArgument(params string[] names) : Argument(true, false, names) {
    public bool Enabled;
    
    public override void Parse(string name, IEnumerator<string> args) {
        Enabled = true;
    }
    
    public static implicit operator bool(FlagArgument value) {
        return value.Enabled;
    }
}
