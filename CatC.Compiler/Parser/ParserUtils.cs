using CatC.Compiler.Ast;
using Sprache;

namespace CatC.Compiler.Parser;

public static class ParserUtils {
    public static (string File, int Line)[]? LineMappings = null;
    
    public static Parser<T> Token<T>(this Parser<T> parser) {
        return
            from leading in CodeParser.Ignored
            from item in parser
            from trailing in CodeParser.Ignored
            select item;
    }
    
    public static Parser<T> WithPosition<T>(this Parser<T> parser) where T : ParsedElement {
        return input => {
            IInput? start = input;
            IResult<T>? result = parser(input);

            if (!result.WasSuccessful) {
                return result;
            }

            string file = "unknown";
            int realLine = start.Line;

            if (LineMappings != null) {
                file = LineMappings[start.Line].File;
                realLine = LineMappings[start.Line].Line;
            }

            return Result.Success(
                result.Value with {
                    FileInformation = new FileInformation(file, realLine, start.Column)
                },
                result.Remainder);
        };
    }
    
    public static Parser<T> WithPositionBad<T>(this Parser<T> parser) where T : ParsedElement {
        return input => {
            IResult<T>? result = parser(input);
            if (result.WasSuccessful) {
                string file = "unknown";
                int realLine = input.Line;
                if (LineMappings != null) {
                    file = LineMappings[input.Line].File;
                    realLine = LineMappings[input.Line].Line;
                }
                
                return Result.Success(result.Value with {
                    FileInformation = new FileInformation(file, realLine, input.Column)
                }, result.Remainder);
            }
            return Result.Failure<T>(input, result.Message, result.Expectations);
        };
    }
}
