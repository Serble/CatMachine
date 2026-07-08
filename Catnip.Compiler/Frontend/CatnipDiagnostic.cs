namespace Catnip.Compiler.Frontend;

public enum CatnipDiagnosticSeverity {
    Error = 1,
    Warning = 2,
    Information = 3,
    Hint = 4
}

public sealed record CatnipSourceRange(
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn);

public sealed record CatnipDiagnostic(
    string File,
    CatnipSourceRange Range,
    string Message,
    CatnipDiagnosticSeverity Severity,
    string? Code = null,
    string? Context = null);
