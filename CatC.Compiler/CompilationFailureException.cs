namespace CatC.Compiler;

public class CompilationFailureException(string file, int line, string msg) : Exception {
    public override string Message => $"[{file}:{line}] {msg}";
}
