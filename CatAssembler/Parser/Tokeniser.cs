using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using CatAssembler.Exceptions;
using CatData;

namespace CatAssembler.Parser;

public partial class Tokeniser {
    public const string LocalLabelPrefix = "__LOCAL_";
    
    private readonly string _file;
    private readonly string[] _contents;
    private int _line;
    private readonly int _lineOffset;
    private string _currentGlobalLabel = "";

    public Tokeniser(string file, string input, int lineOffset = 0) {
        _file = file;
        _contents = input.Split('\n');
        _lineOffset = lineOffset;
    }
    
    public Tokeniser(string file, string[] input, int lineOffset = 0) {
        _file = file;
        _contents = input;
        _lineOffset = lineOffset;
    }
    
    private string TransformLocalLabel(string localLabel) {
        return $"{LocalLabelPrefix}{_currentGlobalLabel}__{localLabel}";
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
        
        Console.WriteLine("Tokenisation complete: " + tokens.Count + " tokens generated.");
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
            
        if (line.EndsWith(':')) {  // label
            string labelName = line[..^1].Trim();
            if (!LabelMatcher().IsMatch(labelName)) {
                Fail($"Invalid label name: {labelName}");
            }
            if (labelName.StartsWith('.')) {
                labelName = TransformLocalLabel(labelName[1..]);
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

    private Token Explain(Token token) {
        return token with {
            File = _file,
            Line = _line + _lineOffset
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
    
    [GeneratedRegex("^.?[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex LabelMatcher();
    
    [GeneratedRegex(@"(?<=\s|,|^)\.[A-Za-z_][A-Za-z0-9_]*")]
    private static partial Regex LocalLabelMatcher();
}
