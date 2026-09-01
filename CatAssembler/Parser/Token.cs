namespace CatAssembler.Parser;

public abstract record Token(string Raw, string File, int Line) {
    /// <summary>
    /// The original high-level source file this token was generated from (set by the
    /// <c>#line</c> directive), or <c>null</c> for hand-written assembly where
    /// <see cref="File"/> is already the original source.
    /// </summary>
    public string? SourceFile { get; init; }

    /// <summary>
    /// 1-based line within <see cref="SourceFile"/>. Only meaningful when
    /// <see cref="SourceFile"/> is non-null.
    /// </summary>
    public int SourceLine { get; init; }
}

public record LabelToken(string Raw, string Name, string File = "", int Line = 0) : Token(Raw, File, Line);

public record DirectiveToken(string Raw, string Name, IExpression[] Args, string File = "", int Line = 0) : Token(Raw, File, Line);

public record InstructionToken(string Raw, string Name, IExpression[] Args, string File = "", int Line = 0) : Token(Raw, File, Line);
