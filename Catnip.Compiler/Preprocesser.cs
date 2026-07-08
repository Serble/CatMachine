using System.Reflection;
using System.Text;
using Catnip.Compiler.Ast;

namespace Catnip.Compiler;

public record PreprocessedResult(
    string[] Lines,
    (string File, int Line)[] LineMappings,
    BinaryGlobal[] BinaryGlobals,
    string[] FileOrder,
    Dictionary<string, string[]> VisibleFilesByFile);

public class Preprocesser {
    private const string MacroUseFormat = "${{{0}}}";

    private static readonly string[] BuiltinLibs = [
        "std.nip",
        "ppu.nip",
        "hardware.nip"
    ];

    private readonly string _mainFileName;
    private readonly string[] _mainLines;

    private readonly Dictionary<string, string> _macros = new();

    // per-file processed output lines with their original (0-based) source line index
    private readonly Dictionary<string, List<(string Line, int SourceLine)>> _processedLinesByFile =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<BinaryGlobal> _binaryGlobals = [];

    // set of files already pulled into the compile scope (dedupe of duplicate includes)
    private readonly HashSet<string> _processedFiles = new(StringComparer.OrdinalIgnoreCase);

    // files in first-encounter order (main -> a -> c -> b)
    private readonly List<string> _fileOrder = [];

    // direct includes per file, used to build each file's visibility closure
    private readonly Dictionary<string, List<string>> _directIncludesByFile =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, string> _virtualFiles;

    public Preprocesser(string mainFileName, string[] lines) {
        _mainFileName = mainFileName;
        _mainLines = lines;
        _virtualFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public Preprocesser(string mainFileName, string text) {
        _mainFileName = mainFileName;
        _mainLines = text.Split(["\n"], StringSplitOptions.None);
        _virtualFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public Preprocesser(string mainFileName, string text, IReadOnlyDictionary<string, string> virtualFiles) {
        _mainFileName = mainFileName;
        _mainLines = text.Split(["\n"], StringSplitOptions.None);
        _virtualFiles = virtualFiles;
    }

    public PreprocessedResult Process() {
        string entryFile = NormalizeMainFileName(_mainFileName);
        ProcessFile(entryFile, _mainLines);

        // flatten by first-encounter file order, without duplicating any file's code
        List<string> lines = [];
        List<(string File, int Line)> lineMappings = [];
        foreach (string file in _fileOrder) {
            if (!_processedLinesByFile.TryGetValue(file, out List<(string Line, int SourceLine)>? fileLines)) {
                continue;
            }

            foreach ((string line, int sourceLine) in fileLines) {
                lines.Add(line);
                lineMappings.Add((file, sourceLine));
            }
        }

        Dictionary<string, string[]> visibleFilesByFile = BuildVisibleFilesByFile();
        return new PreprocessedResult(
            lines.ToArray(),
            lineMappings.ToArray(),
            _binaryGlobals.ToArray(),
            _fileOrder.ToArray(),
            visibleFilesByFile);
    }

    private void ProcessFile(string fileName, IReadOnlyList<string> lines) {
        // duplicate includes must not duplicate code
        if (_processedFiles.Contains(fileName)) {
            return;
        }

        _processedFiles.Add(fileName);
        _fileOrder.Add(fileName);
        _directIncludesByFile.TryAdd(fileName, []);
        _processedLinesByFile.TryAdd(fileName, []);
        List<(string Line, int SourceLine)> outputLines = _processedLinesByFile[fileName];

        for (int i = 0; i < lines.Count; i++) {
            string line = lines[i];

            // macros
            foreach (string macro in _macros.Keys) {
                line = line.Replace(string.Format(MacroUseFormat, macro), _macros[macro]);
            }

            if (!line.StartsWith('#')) {
                outputLines.Add((line, i));
                continue;
            }

            outputLines.Add(("// preprocessor directive processed", i));

            // okay it's a preprocessor directive
            string directiveData = line[1..].Trim();

            // a directive should look like:
            // #directive arg1, arg2, ...
            string[] parts = directiveData.Split(' ', 2,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            string directive = parts[0];
            string argsStr = parts.Length > 1 ? parts[1] : "";

            // turn args into array of strings, split by comma but respecting quotes and brackets
            List<string> args = [];
            int bracketLevel = 0;
            bool inQuotes = false;
            StringBuilder currentArg = new();
            foreach (char c in argsStr) {
                if (c == '"' && (currentArg.Length == 0 || currentArg[^1] != '\\')) {
                    inQuotes = !inQuotes;
                }

                if (!inQuotes) {
                    if (c is '(' or '{' or '[') {
                        bracketLevel++;
                    }
                    else if (c is ')' or '}' or ']') {
                        bracketLevel--;
                    }
                    else if (c == ',' && bracketLevel == 0) {
                        args.Add(currentArg.ToString().Trim());
                        currentArg.Clear();
                        continue;
                    }
                }

                currentArg.Append(c);
            }

            if (currentArg.Length > 0) {
                args.Add(currentArg.ToString().Trim());
            }

            // okay, actually handle directive
            switch (directive) {
                case "define": {
                    if (args.Count != 2) {
                        throw new CompilationFailureException(fileName, i,
                            $"Invalid number of arguments for #define directive: {argsStr}");
                    }

                    string macroName = args[0];
                    string macroValue = args[1];

                    // apply retroactively to everything already emitted in this compile flow
                    foreach (string processedFile in _fileOrder) {
                        if (!_processedLinesByFile.TryGetValue(processedFile,
                                out List<(string Line, int SourceLine)>? processedFileLines)) {
                            continue;
                        }

                        for (int j = 0; j < processedFileLines.Count; j++) {
                            (string existingLine, int existingSource) = processedFileLines[j];
                            processedFileLines[j] =
                                (existingLine.Replace(string.Format(MacroUseFormat, macroName), macroValue), existingSource);
                        }
                    }

                    _macros[macroName] = macroValue;
                    break;
                }

                case "include": {
                    if (args.Count != 1) {
                        throw new CompilationFailureException(fileName, i,
                            $"Invalid number of arguments for #include directive: {argsStr}");
                    }

                    string includePath = args[0].Trim('"');
                    (string[] includedLines, string includeSourceName) = GetLibraryFile(includePath, fileName, i);

                    List<string> includes = _directIncludesByFile[fileName];
                    if (!includes.Contains(includeSourceName, StringComparer.OrdinalIgnoreCase)) {
                        includes.Add(includeSourceName);
                    }

                    ProcessFile(includeSourceName, includedLines);
                    break;
                }

                case "binary": {
                    if (args.Count != 2) {
                        throw new CompilationFailureException(fileName, i,
                            $"Invalid number of arguments for #binary directive: {argsStr}");
                    }

                    string name = args[0];
                    string includePath = ResolveRelativePath(args[1].Trim('"'), fileName);
                    if (!Path.Exists(includePath)) {
                        throw new CompilationFailureException(fileName, i,
                            "Included file not found: " + args[1].Trim('"'));
                    }

                    _binaryGlobals.Add(new BinaryGlobal(name, includePath));
                    break;
                }

                default:
                    throw new CompilationFailureException(fileName, i,
                        "Unknown preprocessor directive: " + directive);
            }
        }
    }

    private Dictionary<string, string[]> BuildVisibleFilesByFile() {
        Dictionary<string, string[]> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (string file in _fileOrder) {
            HashSet<string> closure = new(StringComparer.OrdinalIgnoreCase);
            CollectIncludeClosure(file, closure);
            closure.Add(file);
            result[file] = closure.ToArray();
        }

        return result;
    }

    private void CollectIncludeClosure(string fileName, HashSet<string> closure) {
        if (!_directIncludesByFile.TryGetValue(fileName, out List<string>? directIncludes)) {
            return;
        }

        foreach (string include in directIncludes) {
            if (!closure.Add(include)) {
                continue;
            }

            CollectIncludeClosure(include, closure);
        }
    }

    private static string NormalizeMainFileName(string mainFileName) {
        return Path.GetFullPath(mainFileName);
    }

    private string ResolveRelativePath(string path, string includingFile) {
        if (Path.IsPathRooted(path)) {
            return Path.GetFullPath(path);
        }

        if (Path.Exists(path)) {
            return Path.GetFullPath(path);
        }

        if (Path.IsPathRooted(includingFile)) {
            string includingDirectory = Path.GetDirectoryName(includingFile) ?? Directory.GetCurrentDirectory();
            string candidate = Path.GetFullPath(Path.Combine(includingDirectory, path));
            if (Path.Exists(candidate)) {
                return candidate;
            }
        }

        return Path.GetFullPath(path);
    }

    private (string[] Lines, string SourceName) GetLibraryFile(string name, string includingFile, int includingLine) {
        foreach (string candidate in BuildFileCandidates(name, includingFile)) {
            string fullCandidate = Path.GetFullPath(candidate);
            if (_virtualFiles.TryGetValue(fullCandidate, out string? virtualText)) {
                return (virtualText.Split(["\n"], StringSplitOptions.None), fullCandidate);
            }

            if (!File.Exists(candidate)) {
                continue;
            }

            return (File.ReadAllLines(candidate), Path.GetFullPath(candidate));
        }

        foreach (string builtinCandidate in BuildBuiltinCandidates(name)) {
            if (!BuiltinLibs.Contains(builtinCandidate)) {
                continue;
            }

            return (ReadBuiltin(builtinCandidate), builtinCandidate);
        }

        throw new CompilationFailureException(includingFile, includingLine, "Included file not found: " + name);
    }

    private static List<string> BuildFileCandidates(string name, string includingFile) {
        List<string> candidates = [];

        AddWithAndWithoutExtension(name);
        if (!Path.IsPathRooted(name) && Path.IsPathRooted(includingFile)) {
            string includingDirectory = Path.GetDirectoryName(includingFile) ?? Directory.GetCurrentDirectory();
            AddWithAndWithoutExtension(Path.Combine(includingDirectory, name));
        }

        return candidates;

        void AddWithAndWithoutExtension(string basePath) {
            candidates.Add(basePath);
            if (!basePath.EndsWith(".nip", StringComparison.OrdinalIgnoreCase)) {
                candidates.Add(basePath + ".nip");
            }
        }
    }

    private static IEnumerable<string> BuildBuiltinCandidates(string name) {
        yield return name;
        if (!name.EndsWith(".nip", StringComparison.OrdinalIgnoreCase)) {
            yield return name + ".nip";
        }
    }

    private static string[] ReadBuiltin(string name) {
        Assembly assembly = Assembly.GetExecutingAssembly();

        using Stream? stream = assembly.GetManifestResourceStream($"Catnip.Compiler.Libraries.{name}")
            ?? throw new InvalidOperationException($"Embedded builtin library '{name}' was not found.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd().Split('\n');
    }
}
