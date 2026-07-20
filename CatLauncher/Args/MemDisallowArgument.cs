using CatArgs;
using ArgIterator = CatArgs.ArgIterator;

namespace CatLauncher.Args;

public class MemDisallowArgument(params string[] names) : Argument(false, false, true, false, names) {
    public List<(uint start, uint length)> Regions { get; } = [];
    
    public override void Parse(string? name, ArgIterator args) {
        if (!args.Next(out string? startStr) || !uint.TryParse(startStr, out uint start) ||
            !args.Next(out string? lengthStr) || !uint.TryParse(lengthStr, out uint length)) {
            throw new ArgumentException($"Must have 2 integer values for {Names[0]} argument");
        }

        if (length <= 0) {
            throw new ArgumentException($"Length must be > 0 {Names[0]} argument");
        }

        Regions.Add((start, length));
    }
}
