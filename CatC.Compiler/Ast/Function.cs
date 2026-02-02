namespace CatC.Compiler.Ast;

public record Function(string Name, VarNameSize[] Parameters, IStatement[] Statements);
