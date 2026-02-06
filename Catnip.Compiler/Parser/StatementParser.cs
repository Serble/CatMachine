using Catnip.Compiler.Ast;
using Sprache;

namespace Catnip.Compiler.Parser;

public static class StatementParser {
    
    /*
     * Statement:
     *
     * May be multiple types of statements.
     * 
     */
    private static readonly Parser<Statement> SimpleStatement =
        from leading in CodeParser.Ignored
        from statement in EmptyStatementElement
            .Or(LocalDeclarationElement)
            .Or(GlobalDeclarationElement)
            .Or(ReturnStatementElement)
            .Or(VariableAssignmentElement)
            .Or(
                ExpressionParser.Expression
                    .Where(e => e is FunctionCall)
                    .Select(e => (FunctionCall)e)
            )
        from trailing in CodeParser.Ignored
        select statement;
    
    // TYPES
    
    private static readonly Parser<Statement> EmptyStatementElement =
        from leading in CodeParser.Ignored
        from semicolon in Parse.Char(';').Token()
        from trailing in CodeParser.Ignored
        select (Statement)null!;
    
    // Local Variable Declaration: let NAME:SIZE;
    // may also have initialiser:
    // let NAME:SIZE = VALUE;
    private static readonly Parser<Statement> LocalDeclarationElement =
        DeclarationElement("let", (name, size, initializer) =>
            new LocalDeclaration(name, size, initializer));
    
    // Global Variable Declaration: global NAME:SIZE;
    // may also have initialiser:
    // global NAME:SIZE = VALUE;
    private static readonly Parser<Statement> GlobalDeclarationElement =
        DeclarationElement("global", (name, size, initializer) =>
            new GlobalDeclaration(name, size, initializer));

    private static Parser<Statement> DeclarationElement(string keyword,
        Func<string, CompileTimeValue, IValueExpression?, Statement> cons) {
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
            select cons(name, size, optionalInitializer.GetOrDefault());
    }
    
    // Variable assignment:
    // NAME:SIZE = VALUE;
    private static readonly Parser<Statement> VariableAssignmentElement =
        from leading in CodeParser.Ignored
        from target in ExpressionParser.Expression
        from eq in Parse.Char('=').Token()
        from value in ExpressionParser.Expression
        from trailing in CodeParser.Ignored
        select new VariableAssignment(target, value);
    
    // If statement:
    // if (CONDITION) { STATEMENTS } else { STATEMENTS }
    // else block is optional
    private static readonly Parser<Statement> IfStatementElement =
        from leading in CodeParser.Ignored
        from keyword in Parse.String("if").Token()
        from lparen in Parse.Char('(').Token()
        from condition in ExpressionParser.Expression
        from rparen in Parse.Char(')').Token()
        from thenLBrace in Parse.Char('{').Token()
        from thenStatements in StatementElement.Many()
        from thenOptionalSemicolon in Parse.Char(';').Token().Optional()
        from thenRBrace in Parse.Char('}').Token().Named(CodeParser.FunctionBodyEndExpectation)
        from elsePart in
            (from elseKeyword in Parse.String("else").Token()
             from elseLBrace in Parse.Char('{').Token()
             from elseStatements in StatementElement.Many()
             from elseOptionalSemicolon in Parse.Char(';').Token().Optional()
             from elseRBrace in Parse.Char('}').Token().Named(CodeParser.FunctionBodyEndExpectation)
             select elseStatements).Optional()
        from trailing in CodeParser.Ignored
        select new IfStatement(
            condition,
            thenStatements.ToArray(),
            elsePart.IsDefined 
                ? elsePart.Get().ToArray()
                : []);
    
    // While statement:
    // while (CONDITION) { STATEMENTS }
    private static readonly Parser<Statement> WhileStatementElement =
        from leading in CodeParser.Ignored
        from keyword in Parse.String("while").Token()
        from lparen in Parse.Char('(').Token()
        from condition in ExpressionParser.Expression
        from rparen in Parse.Char(')').Token()
        from lbrace in Parse.Char('{').Token()
        from bodyStatements in StatementElement.Many()
        from optionalSemicolon in Parse.Char(';').Token().Optional()
        from rbrace in Parse.Char('}').Token().Named(CodeParser.FunctionBodyEndExpectation)
        from trailing in CodeParser.Ignored
        select new WhileStatement(condition, bodyStatements.ToArray());
    
    // Return statement:
    // return EXPR;
    // or
    // return;
    private static readonly Parser<Statement> ReturnStatementElement =
        from leading in CodeParser.Ignored
        from keyword in Parse.String("return").Token()
        from returnValue in ExpressionParser.Expression.Optional()
        from trailing in CodeParser.Ignored
        select new ReturnStatement(returnValue.GetOrDefault());
    
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
    private static readonly Parser<Statement> InlineAsmStatementElement =
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
        select new InlineAsm(
            asmCode,
            inputs.IsDefined ? inputs.Get().ToArray() : [],
            outputs.IsDefined ? outputs.Get().ToArray() : [],
            clobbers.IsDefined ? clobbers.Get().ToArray() : []);
    
    // PARSERS FOR STATEMENT TERMINATION
    
    // ';'
    private static readonly Parser<char> Semi =
        Parse.Char(';').Token().Named("';'");

    // The terminated variant: simple statement + its semicolon
    private static readonly Parser<Statement> TerminatedStatement =
        from s in SimpleStatement
        from _ in Semi
        select s;

    private static readonly Parser<Statement> NonTerminatedStatement =
        InlineAsmStatementElement
            .Or(IfStatementElement)
            .Or(WhileStatementElement);

    // One “statement” is either an asm block (no ';') or a terminated statement (with ';')
    public static readonly Parser<Statement> StatementElement =
        NonTerminatedStatement.Or(TerminatedStatement).Named("statement").WithPosition();
}
