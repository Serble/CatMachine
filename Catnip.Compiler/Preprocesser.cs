using System.Text;

namespace Catnip.Compiler;

public class Preprocesser {
    private const string MacroUseFormat = "${{{0}}}";
    
    private readonly string _mainFileName;
    private readonly List<string> _lines = [];
    
    private readonly Dictionary<string, string> _macros = new();
    private List<string> _processedLines = [];
    
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

    public (string[] lines, (string File, int Line)[] lineMappings) Process() {
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
                    if (!File.Exists(includePath)) {
                        throw new CompilationFailureException(GetCurrentLineFile(), GetCurrentLineNumber(), 
                            "Included file not found: " + includePath);
                    }

                    string[] includedLines = File.ReadAllLines(includePath);
                    string fileName = Path.GetFileName(includePath);
                    _lines.InsertRange(i + 1, includedLines);
                    for (int j = includedLines.Length - 1; j >= 0; j--) {
                        _inclusionStack.Push((fileName, j + 1));
                    }
                    break;
                }

                default:
                    throw new CompilationFailureException(GetCurrentLineFile(), GetCurrentLineNumber(), 
                        "Unknown preprocessor directive: " + directive);
            }
        }

        return (_processedLines.ToArray(), _fileLineMapping.ToArray());
        
        string GetCurrentLineFile() {
            return _inclusionStack.Count > 0 ? _inclusionStack.Peek().File : _mainFileName;
        }
        
        int GetCurrentLineNumber() {
            return _inclusionStack.Count > 0 ? _inclusionStack.Peek().Line - 1 : 0;
        }
    }
}
