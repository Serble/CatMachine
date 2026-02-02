namespace CatC.Compiler;

public class CompilationFailureException(string file, int line, string msg) : Exception {
    public override string Message => $"[{file}:{line}] {msg}";

    public CompilationFailureException(string msg) : this("unknown", 0, msg) {
        
    }
}
