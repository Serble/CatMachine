namespace Catnip.Compiler.Ast;

public abstract record Statement(FileInformation? FileInformation) : ParsedElement(FileInformation);

public record LocalDeclaration(
    string Name,
    CompileTimeValue Size,
    IValueExpression? Initial,
    FileInformation? FileInformation = null) : Statement(FileInformation);

public record VariableAssignment(
    IValueExpression Target,
    IValueExpression Value,
    FileInformation? FileInformation = null)
    : Statement(FileInformation);

public record GlobalDeclaration(
    string Name,
    CompileTimeValue Size,
    IValueExpression? Initial,
    FileInformation? FileInformation = null) : Statement(FileInformation);

public record IfStatement(
    IValueExpression Condition,
    Statement[] ThenStatements,
    Statement[] ElseStatements,
    FileInformation? FileInformation = null) : Statement(FileInformation);

public record WhileStatement(
    IValueExpression Condition,
    Statement[] BodyStatements,
    FileInformation? FileInformation = null)
    : Statement(FileInformation);

public record InlineAsm(
    string Asm,
    (string Register, IValueExpression Value)[] Inputs,
    (string Register, IValueExpression Var)[] Outputs,
    string[] Clobbers,
    FileInformation? FileInformation = null) : Statement(FileInformation);

public record ReturnStatement(IValueExpression? Value, FileInformation? FileInformation = null) 
    : Statement(FileInformation);

// don't forget that function calls are also statements.
