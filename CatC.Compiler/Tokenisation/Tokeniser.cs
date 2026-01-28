using System.Text;

namespace CatC.Compiler.Tokenisation;

public class Tokeniser {
    private const string IdentifierChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
    private readonly string[] _lines;
    private readonly string _fileName;
    
    public Tokeniser(string fileName, string[] lines) {
        _lines = lines;
        _fileName = fileName;
    }
    
    public Tokeniser(string fileName, string text) {
        _lines = text.Split(["\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _fileName = fileName;
    }

    public IToken[] Tokenise() {
        List<IToken> tokens = [];

        for (int i = 0; i < _lines.Length; i++) {
            string line = _lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line)) {
                continue;
            }

            if (line.StartsWith("//")) {
                // comment line
                continue;
            }

            StringBuilder currentToken = new();
            for (int j = 0; j < line.Length; j++) {
                char c = line[j];
                if (IdentifierChars.Contains(c)) {
                    currentToken.Append(c);
                    continue;
                }

                if (currentToken.Length > 0) {
                    // end of identifier
                    tokens.Add(new IdentifierToken(currentToken.ToString(), _fileName, i + 1));
                    currentToken.Clear();
                    continue;
                }

                if (c == ' ') {
                    continue;
                }

                if (c == '/' && line[j+1] == '/') {
                    // comment, skip rest of line
                    break;
                }

                switch (c) {
                    case '+':
                        tokens.Add(new SimpleToken(TokenType.Plus, _fileName, i + 1));
                        break;
                    case '-':
                        tokens.Add(new SimpleToken(TokenType.Minus, _fileName, i + 1));
                        break;
                    case '*':
                        tokens.Add(new SimpleToken(TokenType.Asterisk, _fileName, i + 1));
                        break;
                    case '/':
                        tokens.Add(new SimpleToken(TokenType.Slash, _fileName, i + 1));
                        break;
                    case '=':
                        tokens.Add(new SimpleToken(TokenType.Equals, _fileName, i + 1));
                        break;
                    case '(':
                        tokens.Add(new SimpleToken(TokenType.LeftParen, _fileName, i + 1));
                        break;
                    case ')':
                        tokens.Add(new SimpleToken(TokenType.RightParen, _fileName, i + 1));
                        break;
                    case '{':
                        tokens.Add(new SimpleToken(TokenType.LeftBrace, _fileName, i + 1));
                        break;
                    case '}':
                        tokens.Add(new SimpleToken(TokenType.RightBrace, _fileName, i + 1));
                        break;
                    case ',':
                        tokens.Add(new SimpleToken(TokenType.Comma, _fileName, i + 1));
                        break;
                    case ';':
                        tokens.Add(new SimpleToken(TokenType.Semicolon, _fileName, i + 1));
                        break;
                    case '#':
                        tokens.Add(new SimpleToken(TokenType.Hash, _fileName, i + 1));
                        break;
                    case '&':
                        tokens.Add(new SimpleToken(TokenType.And, _fileName, i + 1));
                        break;
                    case '|':
                        tokens.Add(new SimpleToken(TokenType.Or, _fileName, i + 1));
                        break;
                    case '!':
                        tokens.Add(new SimpleToken(TokenType.Not, _fileName, i + 1));
                        break;
                    case '<':
                        tokens.Add(new SimpleToken(TokenType.LessThan, _fileName, i + 1));
                        break;
                    case '>':
                        tokens.Add(new SimpleToken(TokenType.GreaterThan, _fileName, i + 1));
                        break;
                    default:
                        throw new Exception($"Unknown character '{c}' in {_fileName} at line {i + 1}");
                }
            }
        }

        return tokens.ToArray();
    }
}
