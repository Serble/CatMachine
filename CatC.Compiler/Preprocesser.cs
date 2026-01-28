using System.Text;

namespace CatC.Compiler;

public class Preprocesser {
    private readonly string _fileName;
    private readonly string[] _lines;

    public Preprocesser(string fileName, string[] lines) {
        _lines = lines;
        _fileName = fileName;
    }
    
    public Preprocesser(string fileName, string text) {
        _lines = text.Split(["\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _fileName = fileName;
    }

    public string[] Process() {
        List<string> processedLines = [];
        for (int i = 0; i < _lines.Length; i++) {
            string line = _lines[i];
            if (!line.StartsWith('#')) {
                processedLines.Add(line);
                continue;
            }

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
            }

            if (currentArg.Length > 0) {
                args.Add(currentArg.ToString().Trim());
            }

            // okay, actually handle directive
            switch (directive) {
                case "include": {
                    if (args.Count != 1) {
                        throw new Exception("Invalid number of arguments for #include directive");
                    }

                    string includePath = args[0].Trim('"');
                    if (!File.Exists(includePath)) {
                        throw new Exception("Included file not found: " + includePath);
                    }

                    string[] includedLines = File.ReadAllLines(includePath);
                    Preprocesser includedPreprocesser = new(Path.GetFileName(includePath), includedLines);
                    processedLines.AddRange(includedPreprocesser.Process());
                    break;
                }

                default:
                    throw new CompilationFailureException(_fileName, i, "Unknown preprocessor directive: " + directive);
            }
        }

        return processedLines.ToArray();
    }
}
