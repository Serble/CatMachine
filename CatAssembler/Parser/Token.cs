namespace CatAssembler.Parser;

public abstract record Token(string Raw, string File, int Line);

public record LabelToken(string Raw, string Name, string File = "", int Line = 0) : Token(Raw, File, Line);

public record DirectiveToken(string Raw, string Name, IExpression[] Args, string File = "", int Line = 0) : Token(Raw, File, Line);

public record InstructionToken(string Raw, string Name, IExpression[] Args, string File = "", int Line = 0) : Token(Raw, File, Line);
