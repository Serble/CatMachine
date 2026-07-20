using System.Security;
using CatArgs;
using ArgIterator = CatArgs.ArgIterator;

namespace CatLauncher.Args;

public class RomArgument(params string[] names) : Argument(true, false, false, false, names) {
    public string? Path { get; set; }
    public byte[]? Rom { get; set; }
    
    public override void Parse(string? name, ArgIterator args) {
        if (!args.Next(out string? path)) {
            throw new ArgumentException($"File path is required for {Names[0]}");
        }
        
        try {
            Rom = File.ReadAllBytes(path);
            Path = path;
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or DirectoryNotFoundException or
                                       IOException or UnauthorizedAccessException or FileNotFoundException or
                                       SecurityException) {
            throw new ArgumentException("Rom file not found");
        }
    }
}
