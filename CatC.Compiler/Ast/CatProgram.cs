namespace CatC.Compiler.Ast;

public record CatProgram(Struct[] Structs, Statement[] TopLevelStatements, Function[] Functions);

public record VarNameSize(string Name, CompileTimeValue Size);

public record FileInformation(string File, int Line, int Column);

public abstract record ParsedElement(FileInformation? FileInformation);
