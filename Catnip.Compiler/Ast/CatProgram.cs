namespace Catnip.Compiler.Ast;

public record CatProgram(
    Struct[] Structs,
    Statement[] TopLevelStatements,
    Function[] Functions,
    BinaryGlobal[] BinaryGlobals);

public record VarNameSize(string Name, CompileTimeValue Size);

public record FileInformation(string File, int Line, int Column);

public abstract record ParsedElement(FileInformation? FileInformation);

public record BinaryGlobal(string Name, string? FileName, byte[]? Data = null) {
    public BinaryGlobal(string name, byte[] data) : this(name, null, data) { }
}
