using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using CatAssembler.Exceptions;
using CatData;

namespace CatAssembler.Parser;

public partial class Tokeniser {
    public const string LocalLabelPrefix = "__LOCAL_";
    public const string UnscopedGlobalLabelPrefix = "__UNSCOPED_";
    
    private readonly string _file;
    private readonly string[] _contents;
    private int _line;
    private readonly int _lineOffset;
    private string _currentGlobalLabel = "";

    /// <summary>
    /// The high-level source file that the lines being tokenised were generated from, or
    /// <c>null</c> when this is hand-written assembly. Set by the <c>#line</c> directive, or by
    /// the constructor when a whole block of lines shares one origin (macro expansion).
    /// </summary>
    private string? _sourceFile;

    /// <summary>
    /// The line within <see cref="_sourceFile"/> that the assembly being tokenised came from.
    /// One high-level line generally expands to many assembly lines, so this stays fixed until
    /// the next <c>#line</c> rather than advancing with the physical line.
    /// </summary>
    private int _sourceLine;

    public Tokeniser(string file, string input, int lineOffset = 0, string? sourceFile = null, int sourceLine = 0)
        : this(file, input.Split('\n'), lineOffset, sourceFile, sourceLine) { }

    public Tokeniser(string file, string[] input, int lineOffset = 0, string? sourceFile = null, int sourceLine = 0) {
        _file = file;
        _contents = input;
        _lineOffset = lineOffset;
        _sourceFile = sourceFile;
        _sourceLine = sourceLine;
    }
    
    private string TransformLocalLabel(string label) {
        return $"{LocalLabelPrefix}{_currentGlobalLabel}__{label}";
    }

    private string TransformUnscopedGlobalLabel(string label) {
        return $"{UnscopedGlobalLabelPrefix}__{label}";
    }
    
    private string? ReadLine() {
        if (_line >= _contents.Length) {
            return null;
        }
        
        return _contents[_line++].Trim();
    }

    public Token[] Tokenise() {
        List<Token> tokens = [];

        while (ReadToken(tokens)) { }

        return tokens.ToArray();
    }

    private bool ReadToken(List<Token> tokens) {
        string? line = ReadLine();
        if (line == null) {
            return false;
        }

        string raw = line;

        // check for comment at end of line
        int commentIndex = line.IndexOf(';');
        if (commentIndex != -1) {
            line = line[..commentIndex].TrimEnd();
        }
            
        if (string.IsNullOrWhiteSpace(line)) {
            return true;
        }
            

        // ACTUAL TYPES
        
        // globalLabel:           ; globally accessible and locals scope to it
        // .localLabel:           ; only accessible in the same scope
        // @unscopedGlobalLabel:  ; globally accessible but doesn't create a scope
        if (line.EndsWith(':')) {  // label
            string labelName = line[..^1].Trim();
            if (!LabelMatcher().IsMatch(labelName)) {
                Fail($"Invalid label name: {labelName}");
            }
            if (labelName.StartsWith('.')) {
                labelName = TransformLocalLabel(labelName[1..]);
            }
            else if (labelName.StartsWith('$')) {
                labelName = TransformUnscopedGlobalLabel(labelName[1..]);
            }
            else {
                _currentGlobalLabel = labelName;
            }
            
            tokens.Add(Label(raw, labelName));
            return true;
        }

        if (line.StartsWith('#')) {  // directive
            if (line == "#") {
                Fail("Directive name missing");
            }
            string[] parts = line[1..].Split([' '], 2, StringSplitOptions.RemoveEmptyEntries);
            IExpression[] args = parts.Length > 1 ? ParseExpressionList(parts[1]) : [];
                
            if (parts[0].ToLower() == "macro") {
                List<string> lines = [];
                int lineNumber = _line + _lineOffset;
                
                while (true) {
                    line = ReadLine();
                    if (line == null) {
                        Fail("Program cannot end inside of a macro block!");
                    }

                    if (line.StartsWith("#endmacro")) {
                        break;
                    }
                    
                    lines.Add(line);
                }
                
                args = args.Append(new MacroBodyExpression(lines, lineNumber)).ToArray();
            }

            // #line is consumed here rather than in the analyser: it adjusts the location that
            // *subsequent* tokens report, and by the time the analyser runs those tokens have
            // already been given their locations. Handling it per-tokeniser also scopes it
            // correctly, since includes and macro bodies each get their own tokeniser.
            if (parts[0].ToLower() == "line") {
                ApplyLineDirective(args);
                return true;
            }

            tokens.Add(Directive(raw, parts[0], args));
            return true;
        }
        
        // instruction
        string[] instrParts = line.Split([' '], 2, StringSplitOptions.RemoveEmptyEntries);
        IExpression[] instrArgs = instrParts.Length > 1 ? ParseExpressionList(instrParts[1]) : [];
        tokens.Add(Instruction(raw, instrParts[0], instrArgs));
        return true;
    }

    private IExpression[] ParseExpressionList(string input) {
        List<IExpression> exprs = [];
        StringBuilder current = new();

        int parenDepth = 0;
        int inQuotes = 0;

        for (int i = 0; i < input.Length; i++) {
            char c = input[i];
            if (inQuotes != 0) {
                if ((inQuotes == 1 && c == '"') || (inQuotes == 2 && c == '\'')) {
                    inQuotes = 0;
                }

                current.Append(c);
                continue;
            }
            
            switch (c) {
                case '\\':
                    i++; // skip next character
                    continue;
                case ',' when parenDepth == 0:
                    exprs.Add(ParseExpression(current.ToString()));
                    current.Clear();
                    continue;
                case '(':
                    parenDepth++;
                    break;
                case ')':
                    parenDepth--;
                    break;
                case '"':
                    inQuotes = 1;
                    break;
                case '\'':
                    inQuotes = 2;
                    break;
            }

            current.Append(c);
        }

        if (current.Length > 0) {
            exprs.Add(ParseExpression(current.ToString()));
        }

        return exprs.ToArray();
    }

    private IExpression ParseExpression(string text) {
        string raw = text;
        text = text.Trim();
        
        bool pointer = false;
        if (text.StartsWith('@')) {
            pointer = true;
            text = text[1..].Trim();
        } else if (text.StartsWith('[') && text.EndsWith(']')) {
            pointer = true;
            text = text[1..^1].Trim();
        }

        if (string.IsNullOrWhiteSpace(text)) {
            Fail("Empty expression");
        }
        
        if (RegisterExtensions.TryParse(text, out Register reg)) {
            return new RegisterExpression(raw, reg, pointer);
        }
        
        text = LocalLabelMatcher().Replace(text, match => {
            bool isInString = false;
            for (int i = 0; i < match.Index; i++) {
                switch (text[i]) {
                    case '\\': // skip next character
                        i++;
                        continue;
                    case '"':
                        isInString = !isInString;
                        break;
                }
            }
            
            return isInString ? match.Groups[0].Value : TransformLocalLabel(match.Groups[0].Value[1..]);
        });
        
        text = UnscopedGlobalLabelMatcher().Replace(text, match => {
            bool isInString = false;
            for (int i = 0; i < match.Index; i++) {
                switch (text[i]) {
                    case '\\': // skip next character
                        i++;
                        continue;
                    case '"':
                        isInString = !isInString;
                        break;
                }
            }
            
            return isInString ? match.Groups[0].Value : TransformUnscopedGlobalLabel(match.Groups[0].Value[1..]);
        });

        if (!pointer && NameMatcher().IsMatch(text)) {
            return new NameExpression(raw, text);
        }

        Match stringMatch = StringMatcher().Match(text);
        if (!stringMatch.Success) {
            return new NumberExpression(raw, text, pointer);
        }

        if (pointer) {
            Fail("String literals cannot be used as pointers");
        }
        
        // string literal
        text = stringMatch.Groups[1].Value;
        StringBuilder output = new();
        for (int i = 0; i < text.Length; i++) {
            if (text[i] == '\\') {
                i++;
                output.Append(text[i] switch {
                    '\'' => '\'',
                    '\"' => '\"',
                    '\\' => '\\',
                    '0'  => '\0',
                    'a'  => '\a',
                    'b'  => '\b',
                    'f'  => '\f',
                    'n'  => '\n',
                    'r'  => '\r',
                    't'  => '\t',
                    'v'  => '\v',
                    's'  => ' ',
                    _ => throw new ParseException(_file, _line, $"Invalid escape \\{text[i]}")
                });
                
                continue;
            }
            output.Append(text[i]);
        }
        
        return new StringExpression(raw, output.ToString());
    }

    /// <summary>
    /// Handles <c>#line &lt;line&gt;[, "&lt;file&gt;"]</c>, which marks the assembly that follows as
    /// having been generated from the given high-level source location. The mapping applies to
    /// every line until the next <c>#line</c> — it does not advance line by line — because a
    /// single high-level statement usually compiles to many assembly instructions, all of which
    /// belong to that one statement. <c>#line default</c> clears the mapping again.
    /// </summary>
    private void ApplyLineDirective(IExpression[] args) {
        if (args.Length == 0) {
            Fail("#line requires a line number or 'default'");
        }

        if (args.Length > 2) {
            Fail("#line takes at most 2 arguments: <line>[, \"<file>\"]");
        }

        if (args[0] is NameExpression { Value: "default" }) {
            if (args.Length != 1) {
                Fail("#line default does not take a file argument");
            }

            _sourceFile = null;
            _sourceLine = 0;
            return;
        }

        if (args[0] is not NumberExpression number || !int.TryParse(number.Value, out int sourceLine)) {
            Fail("#line requires a literal line number as its first argument");
            return;  // unreachable, Fail throws
        }

        if (sourceLine < 1) {
            Fail("#line requires a positive line number");
        }

        if (args.Length == 2) {
            switch (args[1]) {
                case StringExpression file:
                    _sourceFile = file.Value;
                    break;

                case NameExpression name:
                    _sourceFile = name.Value;
                    break;

                default:
                    Fail("#line requires a string or name expression as its file argument");
                    break;
            }
        }
        else {
            // No file given: keep mapping into whichever file is already in effect, falling back
            // to this one so that `#line N` alone is still meaningful.
            _sourceFile ??= _file;
        }

        _sourceLine = sourceLine;
    }

    private Token Explain(Token token) {
        return token with {
            File = _file,
            Line = _line + _lineOffset,
            SourceFile = _sourceFile,
            SourceLine = _sourceFile == null ? 0 : _sourceLine
        };
    }

    private Token Label(string raw, string name) {
        return Explain(new LabelToken(raw, name));
    }

    private Token Directive(string raw, string name, IExpression[] args) {
        return Explain(new DirectiveToken(raw, name, args));
    }
    
    private Token Instruction(string raw, string name, IExpression[] args) {
        return Explain(new InstructionToken(raw, name, args));
    }

    [DoesNotReturn]
    private void Fail(string msg) {
        throw new ParseException(_file, _line + _lineOffset, msg);
    }

    [GeneratedRegex("""^"((?:(?!.*[^\\]\\[^\\'"0abfnrtvs])(?:[^"]|[^\\]\\")*)(?:[^\\]|\\\\)|)"$""")]
    private static partial Regex StringMatcher();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.]*$")]
    private static partial Regex NameMatcher();

    [GeneratedRegex("^[.$]?[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex LabelMatcher();

    [GeneratedRegex(@"(?<=\s|,|^)\.[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex LocalLabelMatcher();

    [GeneratedRegex(@"(?<=\s|,|^)\$[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex UnscopedGlobalLabelMatcher();
}
