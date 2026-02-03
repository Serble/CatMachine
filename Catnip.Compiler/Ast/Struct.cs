namespace Catnip.Compiler.Ast;

public record Struct(string Name, VarNameSize[] Fields, FileInformation? FileInformation = null) 
    : ParsedElement(FileInformation);

