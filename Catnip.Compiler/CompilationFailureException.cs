using Catnip.Compiler.Ast;

namespace Catnip.Compiler;

public class CompilationFailureException(string file, int line, int column, string msg, string? context = null) : Exception {
    /// <summary>
    /// Expected length of context strings in errors.
    /// </summary>
    public const int ContextLength = 60;
    
    public override string Message => $"[{file}:{line}:{column}] {msg}";
    public string? Context { get; } = context;

    public CompilationFailureException(ParsedElement element, string msg) 
        : this(element.FileInformation?.File ?? "unknown", element.FileInformation?.Line ?? 0, 
            element.FileInformation?.Column ?? 0, msg) { }
    
    public CompilationFailureException(string file, int line, string msg) 
        : this(file, line, 0, msg) { }
}
