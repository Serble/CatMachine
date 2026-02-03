namespace Catnip.Compiler.Ast;

public record Function(string Name, 
    VarNameSize[] Parameters, 
    Statement[] Statements, 
    FileInformation? FileInformation = null) 
    : ParsedElement(FileInformation);
