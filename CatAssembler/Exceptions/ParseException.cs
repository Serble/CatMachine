namespace CatAssembler.Exceptions;

public class ParseException(string file, int line, string msg) : Exception {
    public override string Message => $"[{file}:{line}] {msg}";
}
