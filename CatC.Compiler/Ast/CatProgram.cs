namespace CatC.Compiler.Ast;

public record CatProgram(Struct[] Structs, IStatement[] TopLevelStatements, Function[] Functions);

public record VarNameSize(string Name, CompileTimeValue Size);

