namespace CatC.Compiler.Ast;

public interface IStatement;

public record LocalDeclaration(string Name, CompileTimeValue Size, IValueExpression? Initial) : IStatement;

public record VariableAssignment(IValueExpression Target, IValueExpression Value) : IStatement;

public record GlobalDeclaration(string Name, CompileTimeValue Size, IValueExpression? Initial) : IStatement;

public record IfStatement(IValueExpression Condition, IStatement[] ThenStatements, IStatement[] ElseStatements) : IStatement;

public record WhileStatement(IValueExpression Condition, IStatement[] BodyStatements) : IStatement;

public record InlineAsm(string Asm, (string Register, IValueExpression Value)[] Inputs, 
    (string Register, IValueExpression Var)[] Outputs, string[] Clobbers) : IStatement;

public record ReturnStatement(IValueExpression? Value) : IStatement;

// don't forget that function calls are also statements.
