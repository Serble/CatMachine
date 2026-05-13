using CatVM;

namespace CatLauncher.Args;

public class MemDisallowArgument(params string[] names) : Argument(false, true, names) {
    public List<(uint start, uint length)> Regions { get; } = [];
    
    public override void Parse(string name, IEnumerator<string> args) {
        if (!CatVm.DebugMode) {
            throw new ArgumentException($"The CatVM must be build with the debug flag for you to use {Names[0]}");
        }
        
        if (!args.MoveNext() || !uint.TryParse(args.Current, out uint start) ||
            !args.MoveNext() || !uint.TryParse(args.Current, out uint length)) {
            throw new ArgumentException($"Must have 2 integer values for {Names[0]} argument");
        }

        if (start < 0) {
            throw new ArgumentException($"Start must be >= 0 {Names[0]} argument");
        }
        
        if (length <= 0) {
            throw new ArgumentException($"Length must be > 0 {Names[0]} argument");
        }

        Regions.Add((start, length));
    }
}
