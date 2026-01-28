namespace CatC.Compiler.Tokenisation;

public interface IToken {
    TokenType Type { get; }
    string File { get; }
    int Line { get; }
}

public enum TokenType {
    Identifier,
    Number,
    StringLiteral,
    Plus,
    Minus,
    Asterisk,
    Slash,
    Equals,
    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    Comma,
    Semicolon,
    Hash,
    And,
    Or,
    Not,
    LessThan,
    GreaterThan
}

public record SimpleToken(TokenType Type, string File, int Line) : IToken;

public record IdentifierToken(string Name, string File, int Line) : IToken {
    public TokenType Type => TokenType.Identifier;
}
