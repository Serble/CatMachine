using CatC.Compiler.Ast;

namespace CatC.Compiler.Parser;

public abstract record ParsedElement;

public record ParsedStatementElement(IStatement Statement) : ParsedElement;

// public record ParsedValueExpressionElement(ValueExpression Expression) : ParsedElement;

public record ParsedStructElement(Struct Struct) : ParsedElement;

public record ParsedFunctionElement(Function Function) : ParsedElement;
