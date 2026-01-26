namespace CatAssembler.Parser;

public abstract record Token(string File, int Line);

public record LabelToken(string Name, string File = "", int Line = 0) : Token(File, Line);

public record DirectiveToken(string Name, IExpression[] Args, string File = "", int Line = 0) : Token(File, Line);

public record InstructionToken(string Name, IExpression[] Args, string File = "", int Line = 0) : Token(File, Line);

