using System.Reflection;
using System.Text;
using Catnip.Compiler.Ast;

namespace Catnip.Compiler;

public record PreprocessedResult(
    string[] Lines, 
    (string File, int Line)[] LineMappings, 
    BinaryGlobal[] BinaryGlobals);

public class Preprocesser {
    private const string MacroUseFormat = "${{{0}}}";

    private static readonly string[] BuiltinLibs = [
        "std.nip"
    ];
    
    private readonly string _mainFileName;
    private readonly List<string> _lines = [];
    
    private readonly Dictionary<string, string> _macros = new();
    private readonly List<string> _processedLines = [];
    
    private readonly List<BinaryGlobal> _binaryGlobals = [];
    
    private readonly List<(string File, int Line)> _fileLineMapping = [];
    private readonly Stack<(string File, int Line)> _inclusionStack = [];

    public Preprocesser(string mainFileName, string[] lines) {
        _lines.AddRange(lines);
        _mainFileName = mainFileName;
    }
    
    public Preprocesser(string mainFileName, string text) {
        _lines.AddRange(text.Split(["\n"], StringSplitOptions.None));
        _mainFileName = mainFileName;
    }

    public PreprocessedResult Process() {
        // initialize inclusion stack
        for (int i = _lines.Count - 1; i >= 0; i--) {
            _inclusionStack.Push((_mainFileName, i + 1));
        }
        
        for (int i = 0; i < _lines.Count; i++) {
            string line = _lines[i];
            
            // record file/line mapping
            (string currentFile, int currentLine) = _inclusionStack.Pop();
            _fileLineMapping.Add((currentFile, currentLine-1));
            
            // macros
            foreach (string macro in _macros.Keys) {
                line = line.Replace(string.Format(MacroUseFormat, macro), _macros[macro]);
            }
            
            if (!line.StartsWith('#')) {
                _processedLines.Add(line);
                continue;
            }
            _processedLines.Add("// preprocessor directive processed");

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
                        throw new CompilationFailureException(GetCurrentLineFile(), GetCurrentLineNumber(), 
                            $"Invalid number of arguments for #define directive: {argsStr}");
                    }

                    string macroName = args[0];
                    string macroValue = args[1];
                    for (int j = 0; j < _processedLines.Count; j++) {
                        _processedLines[j] = _processedLines[j].Replace(string.Format(MacroUseFormat, macroName), macroValue);
                    }
                    _macros[macroName] = macroValue;
                    break;
                }
                
                case "include": {
                    if (args.Count != 1) {
                        throw new CompilationFailureException(GetCurrentLineFile(), GetCurrentLineNumber(), 
                            $"Invalid number of arguments for #include directive: {argsStr}");
                    }

                    string includePath = args[0].Trim('"');
                    string[] includedLines = GetLibraryFile(includePath);
                    string fileName = Path.GetFileName(includePath);
                    _lines.InsertRange(i + 1, includedLines);
                    for (int j = includedLines.Length - 1; j >= 0; j--) {
                        _inclusionStack.Push((fileName, j + 1));
                    }
                    break;
                }

                case "binary": {
                    if (args.Count != 2) {
                        throw new CompilationFailureException(GetCurrentLineFile(), GetCurrentLineNumber(), 
                            $"Invalid number of arguments for #binary directive: {argsStr}");
                    }

                    string name = args[0];
                    string includePath = args[1].Trim('"');
                    if (!Path.Exists(includePath)) {
                        throw new CompilationFailureException(GetCurrentLineFile(), GetCurrentLineNumber(), 
                            "Included file not found: " + includePath);
                    }

                    _binaryGlobals.Add(new BinaryGlobal(name, includePath));
                    break;
                }

                default:
                    throw new CompilationFailureException(GetCurrentLineFile(), GetCurrentLineNumber(), 
                        "Unknown preprocessor directive: " + directive);
            }
        }

        return new PreprocessedResult(_processedLines.ToArray(), _fileLineMapping.ToArray(), _binaryGlobals.ToArray());
    }
    
    private string GetCurrentLineFile() {
        return _inclusionStack.Count > 0 ? _inclusionStack.Peek().File : _mainFileName;
    }
        
    private int GetCurrentLineNumber() {
        return _inclusionStack.Count > 0 ? _inclusionStack.Peek().Line - 1 : 0;
    }

    private string[] GetLibraryFile(string name) {
        string originalName = name;
        
        if (File.Exists(name)) {
            return File.ReadAllLines(name);
        }

        if (BuiltinLibs.Contains(name)) {
            return ReadBuiltin(name);
        }
        
        name += ".nip";
        if (File.Exists(name)) {
            return File.ReadAllLines(name);
        }
        
        if (BuiltinLibs.Contains(name)) {
            return ReadBuiltin(name);
        }

        throw new CompilationFailureException(GetCurrentLineFile(), GetCurrentLineNumber(), 
            "Included file not found: " + originalName);
    }

    private static string[] ReadBuiltin(string name) {
        Assembly assembly = Assembly.GetExecutingAssembly();

        using Stream stream = assembly.GetManifestResourceStream($"Catnip.Compiler.Libraries.{name}")!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd().Split('\n');
    }
}
