using Catnip.Compiler.Ast;
using Sprache;

namespace Catnip.Compiler.Parser;

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
    
    /// <summary>
    /// Pairs a parsed value with where it started, mapped through the preprocessor's line table
    /// the same way <see cref="WithPosition{T}"/> is — for the parts of an element that are not
    /// elements themselves, such as a struct's fields, so they can be located on their own.
    /// </summary>
    public static Parser<(T Value, FileInformation Position)> Positioned<T>(this Parser<T> parser) {
        return input => {
            IInput start = input;
            IResult<T> result = parser(input);
            if (!result.WasSuccessful) {
                return Result.Failure<(T, FileInformation)>(result.Remainder, result.Message, result.Expectations);
            }

            string file = "unknown";
            int realLine = start.Line;
            if (LineMappings != null) {
                file = LineMappings[start.Line].File;
                realLine = LineMappings[start.Line].Line;
            }

            return Result.Success(
                (result.Value, new FileInformation(file, realLine, start.Column)),
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
