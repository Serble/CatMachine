using Sprache;

namespace CatC.Compiler.Parser;

public static class ParserUtils {
    public static Parser<T> Token<T>(this Parser<T> parser) {
        return
            from leading in CodeParser.Ignored
            from item in parser
            from trailing in CodeParser.Ignored
            select item;
    }
}
