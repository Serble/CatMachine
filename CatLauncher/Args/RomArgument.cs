using System.Security;

namespace CatLauncher.Args;

public class RomArgument(params string[] names) : Argument(false, false, names) {
    public string? Path { get; set; }
    public byte[]? Rom { get; set; }
    
    public override void Parse(string name, IEnumerator<string> args) {
        if (!args.MoveNext()) {
            throw new ArgumentException($"File path is required for {Names[0]}");
        }

        Path = args.Current;
        try {
            Rom = File.ReadAllBytes(Path);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or DirectoryNotFoundException or
                                       IOException or UnauthorizedAccessException or FileNotFoundException or
                                       SecurityException) {
            throw new ArgumentException("Rom file not found");
        }
    }
}