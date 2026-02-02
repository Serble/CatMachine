using CatC.Compiler.Ast;
using IntegerMaths;
using Sprache;

namespace CatC.Compiler.Parser;

public static class CodeParser {
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
        from element in StructElement
            .Or(FunctionElement.Select(ParsedElement (e) => e))
            .Or(StatementParser.StatementElement.Select(ParsedElement (e) => e))
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
    private static readonly Parser<ParsedFunctionElement> FunctionElement =
        from leading in Ignored
        from keyword in Parse.String("fun").Token()
        from name in Parse.Letter.Or(Parse.Char('_'))
            .Then(first => Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
                .Select(rest => first + new string(rest.ToArray()))).Token()
        from lparen in Parse.Char('(').Token()
        from parameters in VariableNameSizeSpec.DelimitedBy(Parse.Char(',').Token()).Optional()
        from rparen in Parse.Char(')').Token()
        from lbrace in Parse.Char('{').Token()
        from body in StatementParser.StatementElement.DelimitedBy(Parse.Char(';').Token())
            .Optional()
        from optionalSemicolon in Parse.Char(';').Token().Optional()
        from rbrace in Parse.Char('}').Token()
        from trailing in Ignored
        select new ParsedFunctionElement(new Function(name, parameters.IsDefined ? parameters.Get().ToArray() : [], 
            body.IsDefined ? body.Get().Select(e => e.Statement).ToArray() : []));
    
    /*
     * Struct:
     *
     * struct NAME {
     *     MEMBER:SIZE;
     *     MEMBER:SIZE;
     *     MEMBER:SIZE;
     * }
     */
    private static readonly Parser<ParsedStructElement> StructElement =
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
        select new ParsedStructElement(new Struct(name, members.ToArray()));
    
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

    public static readonly Parser<ParsedElement[]> Program = Element.AtLeastOnce().Select(elements => {
        return elements.Where(e => e != null).ToArray();
    }).End();
}
