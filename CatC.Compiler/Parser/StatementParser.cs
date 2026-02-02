using CatC.Compiler.Ast;
using Sprache;

namespace CatC.Compiler.Parser;

public static class StatementParser {
    
    /*
     * Statement:
     *
     * May be multiple types of statements.
     * 
     */
    public static readonly Parser<ParsedStatementElement> StatementElement =
        from leading in CodeParser.Ignored
        from statement in EmptyStatementElement
            .Or(LocalDeclarationElement.Select(ParsedStatementElement (e) => e))
            .Or(GlobalDeclarationElement.Select(ParsedStatementElement (e) => e))
            .Or(IfStatementElement.Select(ParsedStatementElement (e) => e))
            .Or(WhileStatementElement.Select(ParsedStatementElement (e) => e))
            .Or(InlineAsmStatementElement.Select(ParsedStatementElement (e) => e))
            .Or(ReturnStatementElement.Select(ParsedStatementElement (e) => e))
            .Or(VariableAssignmentElement.Select(ParsedStatementElement (e) => e))
            .Or(
                ExpressionParser.Expression
                    .Where(e => e is FunctionCall)
                    .Select(e => new ParsedStatementElement((FunctionCall)e))
            )
        from trailing in CodeParser.Ignored
        select statement;
    
    private static readonly Parser<ParsedStatementElement> EmptyStatementElement =
        from leading in CodeParser.Ignored
        from semicolon in Parse.Char(';').Token()
        from trailing in CodeParser.Ignored
        select (ParsedStatementElement)null!;
    
    // Local Variable Declaration: let NAME:SIZE;
    // may also have initialiser:
    // let NAME:SIZE = VALUE;
    private static readonly Parser<ParsedStatementElement> LocalDeclarationElement =
        DeclarationElement("let", (name, size, initializer) =>
            new LocalDeclaration(name, size, initializer));
    
    // Global Variable Declaration: global NAME:SIZE;
    // may also have initialiser:
    // global NAME:SIZE = VALUE;
    private static readonly Parser<ParsedStatementElement> GlobalDeclarationElement =
        DeclarationElement("global", (name, size, initializer) =>
            new GlobalDeclaration(name, size, initializer));

    private static Parser<ParsedStatementElement> DeclarationElement(string keyword,
        Func<string, CompileTimeValue, IValueExpression?, IStatement> cons) {
        return from leading in CodeParser.Ignored
            from gkeyword in Parse.String(keyword).Token()
            from name in Parse.Letter.Or(Parse.Char('_'))
                .Then(first => Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
                    .Select(rest => first + new string(rest.ToArray()))).Token()
            from colon in Parse.Char(':').Token()
            from size in CodeParser.CompileTimeValueParser
            from optionalInitializer in
                (from eq in Parse.Char('=').Token()
                    from value in ExpressionParser.Expression
                    select value).Optional()
            from trailing in CodeParser.Ignored
            select new ParsedStatementElement(cons(name, size,
                optionalInitializer.GetOrDefault()));
    }
    
    // Variable assignment:
    // NAME:SIZE = VALUE;
    private static readonly Parser<ParsedStatementElement> VariableAssignmentElement =
        from leading in CodeParser.Ignored
        from target in ExpressionParser.Expression
        from eq in Parse.Char('=').Token()
        from value in ExpressionParser.Expression
        from trailing in CodeParser.Ignored
        select new ParsedStatementElement(new VariableAssignment(target, value));
    
    // If statement:
    // if (CONDITION) { STATEMENTS } else { STATEMENTS }
    // else block is optional
    private static readonly Parser<ParsedStatementElement> IfStatementElement =
        from leading in CodeParser.Ignored
        from keyword in Parse.String("if").Token()
        from lparen in Parse.Char('(').Token()
        from condition in ExpressionParser.Expression
        from rparen in Parse.Char(')').Token()
        from thenLBrace in Parse.Char('{').Token()
        from thenStatements in StatementElement.DelimitedBy(Parse.Char(';').Token()).Optional()
        from thenOptionalSemicolon in Parse.Char(';').Token().Optional()
        from thenRBrace in Parse.Char('}').Token()
        from elsePart in
            (from elseKeyword in Parse.String("else").Token()
             from elseLBrace in Parse.Char('{').Token()
             from elseStatements in StatementElement.DelimitedBy(Parse.Char(';').Token()).Optional()
             from elseOptionalSemicolon in Parse.Char(';').Token().Optional()
             from elseRBrace in Parse.Char('}').Token()
             select elseStatements).Optional()
        from trailing in CodeParser.Ignored
        select new ParsedStatementElement(new IfStatement(
            condition,
            thenStatements.IsDefined 
                ? thenStatements.Get().Select(e => e.Statement).ToArray() 
                : [],
            elsePart.IsDefined 
                ? elsePart.Get().IsDefined 
                    ? elsePart.Get().Get().Select(e => e.Statement).ToArray() 
                    : [] 
                : []));
    
    // While statement:
    // while (CONDITION) { STATEMENTS }
    private static readonly Parser<ParsedStatementElement> WhileStatementElement =
        from leading in CodeParser.Ignored
        from keyword in Parse.String("while").Token()
        from lparen in Parse.Char('(').Token()
        from condition in ExpressionParser.Expression
        from rparen in Parse.Char(')').Token()
        from lbrace in Parse.Char('{').Token()
        from bodyStatements in StatementElement.DelimitedBy(Parse.Char(';').Token()).Optional()
        from optionalSemicolon in Parse.Char(';').Token().Optional()
        from rbrace in Parse.Char('}').Token()
        from trailing in CodeParser.Ignored
        select new ParsedStatementElement(new WhileStatement(
            condition,
            bodyStatements.IsDefined 
                ? bodyStatements.Get().Select(e => e.Statement).ToArray()
                : []));
    
    // Return statement:
    // return EXPR;
    // or
    // return;
    private static readonly Parser<ParsedStatementElement> ReturnStatementElement =
        from leading in CodeParser.Ignored
        from keyword in Parse.String("return").Token()
        from returnValue in ExpressionParser.Expression.Optional()
        from trailing in CodeParser.Ignored
        select new ParsedStatementElement(new ReturnStatement(returnValue.GetOrDefault()));
    
    // Inline ASM statement:
    // ~~~register[INPUT EXPR], register[INPUT EXPR] ... | register[OUTPUT EXPR] | clobber, clobber2 ...;
    // ASM LITERAL STRING
    // MORE ASM
    // ...
    // ~~~
    // The literal string should include newlines as appropriate.
    // register is a string that is a valid register name.
    // INPUT EXPR is an expression that evaluates to the input value for that register.
    // OUTPUT EXPR must evaluate to a variable (myVar:SIZE) that will receive the output value from that register.
    // clobber is a register name that is clobbered by the ASM code. (unquoted)
    // There may be zero or more inputs, outputs, and clobbers.
    // The ASM code is everything between the ~~~ markers.
    // If there are no inputs, outputs, or clobbers, the corresponding sections are empty but the | separators are still present.
    private static readonly Parser<ParsedStatementElement> InlineAsmStatementElement =
        from leading in CodeParser.Ignored
        from startMarker in Parse.String("~~~").Token()
        from inputs in
            (from input in
                from register in Parse.Letter.Or(Parse.Char('_'))
                    .Then(first => Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
                        .Select(rest => first + new string(rest.ToArray()))).Token()
                from lbracket in Parse.Char('[').Token()
                from expr in ExpressionParser.Expression
                from rbracket in Parse.Char(']').Token()
                select (register, expr)
             select input).DelimitedBy(Parse.Char(',').Token()).Optional()
        from pipe1 in Parse.Char('|').Token()
        from outputs in
            (from output in
                from register in Parse.Letter.Or(Parse.Char('_'))
                    .Then(first => Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
                        .Select(rest => first + new string(rest.ToArray()))).Token()
                from lbracket in Parse.Char('[').Token()
                from expr in ExpressionParser.Expression
                from rbracket in Parse.Char(']').Token()
                select (register, expr)
             select output).DelimitedBy(Parse.Char(',').Token()).Optional()
        from pipe2 in Parse.Char('|').Token()
        from clobbers in
            (from clobber in Parse.Letter.Or(Parse.Char('_'))
                .Then(first => Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
                    .Select(rest => first + new string(rest.ToArray()))).Token()
             select clobber).DelimitedBy(Parse.Char(',').Token()).Optional()
        from semicolon in Parse.Char(';').Token()
        from asmCode in Parse.AnyChar.Except(Parse.String("~~~")).Many().Text()
        from endMarker in Parse.String("~~~").Token()
        from trailing in CodeParser.Ignored
        select new ParsedStatementElement(new InlineAsm(
            asmCode,
            inputs.IsDefined ? inputs.Get().ToArray() : [],
            outputs.IsDefined ? outputs.Get().ToArray() : [],
            clobbers.IsDefined ? clobbers.Get().ToArray() : []));
}
