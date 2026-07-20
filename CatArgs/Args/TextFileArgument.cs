using System.Security;

namespace CatArgs.Args;

public class TextFileArgument(bool required, bool positional, string[] names, string? defaultPath = null, string? defaultContents = null) : Argument(required, false, false, positional, names) {
    public string? Path { get; set; } = defaultPath;

    public string? Contents {
        get {
            if (field != null) {
                return field;
            }

            if (Path == null) {
                return null;
            }

            try {
                LoadContents(Path);
                return field;
            }
            catch (ArgumentException) {
                return null;
            }
        }

        set;
    } = defaultContents;

    public override void Parse(string? name, ArgIterator args) {
        if (!args.Next(out string? path)) {
            throw new ArgumentException($"File path is required for {Names[0]}");
        }

        Path = path;
        LoadContents(path);
    }

    private void LoadContents(string path) {
        try {
            Contents = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or DirectoryNotFoundException or
                                       IOException or UnauthorizedAccessException or FileNotFoundException or
                                       SecurityException) {
            throw new ArgumentException(names[0] + ": File could not be read");
        }
    }
}
