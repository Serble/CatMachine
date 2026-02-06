using Catnip.Compiler.Ast;
using IntegerMaths;
using Sprache;

namespace Catnip.Compiler.Parser;

public static class CodeParser {
    public const string FunctionBodyEndExpectation = "block body end ('}')";
    
    /*
     * Comments:
     * Single line: //
     * Multi line: /* * /
     */
    private static readonly Parser<string> LineComment =
        from start in Parse.String("//")
        from comment in Parse.CharExcept('\n').Many()
        select new string(comment.ToArray());

    private static readonly Parser<string> BlockComment =
        from open in Parse.String("/*")
        from content in Parse.AnyChar
            .Many()
            .Text()
            .Until(Parse.String("*/"))
        from close in Parse.String("*/")
        select content.ToString();
    
    public static readonly Parser<object?> Ignored = Parse.WhiteSpace
        .Or(LineComment.Select(_ => ' '))
        .Or(BlockComment.Select(_ => ' ')).Many().Return((object?)null);  // Many() ignores as many as possible

    // $STRUCTNAME
    public static readonly Parser<CompileTimeValue> StructSizeValue =
        from leading in Ignored
        from dollar in Parse.Char('$').Token()
        from structName in Parse.Letter.Or(Parse.Char('_'))
            .Then(first => Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
                .Select(rest => first + new string(rest.ToArray()))).Token()
        from trailing in Ignored
        select new CompileTimeStructSize(structName);
    
    public static readonly Parser<CompileTimeValue> CompileTimeValueParser =
        from leading in Ignored
        from number in MathsParser.UnsignedNumber
            .Select(v => new CompileTimeNumber(v.LiteralValue().ToUInt32WithOverflow()))
            .Or(StructSizeValue)
        select number;
    
    /*
     * Element:
     * May be Struct, Function, Statement, etc.
     */
    private static readonly Parser<ParsedElement> Element =
        from leading in Ignored
        from element in Parse.Ref(() => StructElement).WithPosition().Named("struct")
            .XOr(Parse.Ref(() => FunctionElement).WithPosition()).Named("function")
            .XOr(StatementParser.StatementElement)  // it calls .WithPosition() internally
            // .Named("element (struct, fun, or statement)")
        from trailing in Ignored
        select element;
    
    
    
    /*
     * Function Definition:
     * 
     * fun NAME(PARAM:SIZE, PARAM:SIZE) {
     *    STATEMENT;
     *    STATEMENT;
     * }
     *
     * parameters and statements may be zero or more
     */
    private static readonly Parser<ParsedElement> FunctionElement =
        from leading in Ignored
        from keyword in Parse.String("fun").Token()
        from name in Parse.Letter.Or(Parse.Char('_'))
            .Then(first => Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
                .Select(rest => first + new string(rest.ToArray()))).Token().Named("function name")
        from lparen in Parse.Char('(').Token().Named("function parameters start '('")
        from parameters in VariableNameSizeSpec.DelimitedBy(Parse.Char(',').Token()).Optional()
        from rparen in Parse.Char(')').Token().Named("function parameters end ')'")
        from lbrace in Parse.Char('{').Token().Named("function body start '{'")
        from body in StatementParser.StatementElement.Many()
            .Optional()
        from optionalSemicolon in Parse.Char(';').Token().Optional()
        from rbrace in Parse.Char('}').Token().Named(FunctionBodyEndExpectation)
        from trailing in Ignored
        select new Function(name, parameters.IsDefined ? parameters.Get().ToArray() : [], 
            body.IsDefined ? body.Get().ToArray() : []);
    
    /*
     * Struct:
     *
     * struct NAME {
     *     MEMBER:SIZE;
     *     MEMBER:SIZE;
     *     MEMBER:SIZE;
     * }
     */
    private static readonly Parser<Struct> StructElement =
        from leading in Ignored
        from keyword in Parse.String("struct").Token()
        from name in Parse.Letter.Or(Parse.Char('_'))
            .Then(first => Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
                .Select(rest => first + new string(rest.ToArray()))).Token()
        from lbrace in Parse.Char('{').Token()
        from members in VariableNameSizeSpec.DelimitedBy(Parse.Char(';').Token())
        from optionalSemicolon in Parse.Char(';').Token().Optional()
        from rbrace in Parse.Char('}').Token()
        from trailing in Ignored
        select new Struct(name, members.ToArray());
    
    // NAME:SIZE
    private static readonly Parser<VarNameSize> VariableNameSizeSpec =
        from leading in Ignored
        from memberName in Parse.Letter.Or(Parse.Char('_'))
            .Then(first => Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
                .Select(rest => first + new string(rest.ToArray()))).Token()
        from colon in Parse.Char(':').Token()
        from size in CompileTimeValueParser
        from trailing in Ignored
        select new VarNameSize(memberName, size);

    public static readonly Parser<IEnumerable<ParsedElement>> Program = Element.AtLeastOnce().End();
    
    public static ParsedElement[] ParseCode(string code, (string File, int Line)[] lineMappings) {
        ParserUtils.LineMappings = lineMappings;
        
        IResult<IEnumerable<ParsedElement>> parseResult = Program.TryParse(code);
        if (parseResult.WasSuccessful) return parseResult.Value.Where(e => e != null!).ToArray();
        
        // error, let's try and get as specific as possible
        // so we'll reparse just the last component that failed
        IResult<ParsedElement> element = Element(parseResult.Remainder);
        if (element.WasSuccessful) throw new Exception("Unreachable code reached in CodeParser.ParseCode");
        
        // okay it failed, let's see if we can dig deeper
        // did we fail on a function statement?
        while (element.Expectations.Contains(FunctionBodyEndExpectation)) {
            // we failed in a statement most likely
            IResult<ParsedElement> statementElement = StatementParser.StatementElement(element.Remainder);
            if (!statementElement.WasSuccessful) {
                // failed in statement parsing
                element = statementElement;
            }
        }
        
        // perform mapping to original file/line
        string fileName = lineMappings[element.Remainder.Line].File;
        int realLine = lineMappings[element.Remainder.Line].Line;
        
        // generate context for the error
        // get CompilationFailureException.ContextLength characters before the error position
        // and go until 1 character after the error position
        // except round the start to a line boundary
        int contextStart = element.Remainder.Position - CompilationFailureException.ContextLength;
        if (contextStart < 0) contextStart = 0;
        else {
            // round to line boundary
            int lastNewline = code.LastIndexOf('\n', contextStart);
            if (lastNewline != -1) contextStart = lastNewline + 1;
        }
        int contextEnd = element.Remainder.Position + 1;
        if (contextEnd > code.Length) contextEnd = code.Length;
        string context = code.Substring(contextStart, contextEnd - contextStart) + "  <--- Here";
        
        // modify context to include line numbers at the start
        string[] contextLines = context.Split('\n');
        for (int i = 0; i < contextLines.Length; i++) {
            contextLines[i] = $"{realLine - (contextLines.Length - 1 - i)} | {contextLines[i]}";
        }
        context = string.Join('\n', contextLines);
        
        throw new CompilationFailureException(fileName, realLine, element.Remainder.Column, 
            $"Failed to parse code: {element.Message}, expected {string.Join(" or ", element.Expectations)}", context);

    }
}
