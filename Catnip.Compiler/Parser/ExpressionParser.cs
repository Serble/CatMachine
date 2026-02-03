using Catnip.Compiler.Ast;
using IntegerMaths;
using Sprache;

namespace Catnip.Compiler.Parser;

public static class ExpressionParser {

    // Parentheses expression
    private static readonly Parser<IValueExpression> Parens =
        from lparen in Parse.Char('(').Token()
        from expr in Parse.Ref(() => Expression)
        from rparen in Parse.Char(')').Token()
        select expr;
    
    // String literal
    // "Hello"
    // "Hello\nWorld"
    // "\"hello\""
    private static readonly Parser<IValueExpression> StringLiteral =
        from openQuote in Parse.Char('"')
        from content in Parse.CharExcept(ch => ch is '"' or '\\', "quote or backslash")
            .Or(Parse.Char('\\').Then(_ => Parse.AnyChar.Select(escaped => escaped switch {
                '\'' => '\'',
                '\"' => '\"',
                '\\' => '\\',
                '0'  => '\0',
                'a'  => '\a',
                'b'  => '\b',
                'f'  => '\f',
                'n'  => '\n',
                'r'  => '\r',
                't'  => '\t',
                'v'  => '\v',
                's'  => ' ',
                _ => escaped
            })))
            .Many()
            .Text()
        from closeQuote in Parse.Char('"')
        select new StringLiteral(content);
    
    // Variable without size ('variable token')
    // eg. just 'varName'
    public static readonly Parser<IValueExpression> VariableToken =
        from varName in Parse.Letter.Or(Parse.Char('_'))
            .Then(first => Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
                .Select(rest => first + new string(rest.ToArray()))).Token()
        select new VariableToken(varName);
    
    // Struct member
    // MyStruct#member
    private static readonly Parser<IValueExpression> StructMember =
        from structVar in VariableToken
        from hash in Parse.Char('#').Token()
        from memberName in Parse.Letter.Or(Parse.Char('_'))
            .Then(first => Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
                .Select(rest => first + new string(rest.ToArray()))).Token()
        select new StructOffsetOf(((VariableToken)structVar).Name, memberName);

    // Atom: either a number or a parenthesized subexpression
    private static readonly Parser<IValueExpression> Atom =
        Parens
            .Or(MathsParser.UnsignedNumber.Where(e => e is Literal).Select(n => 
                new IntegerLiteral(n.LiteralValue().ToUInt32WithOverflow())))
            .Or(CodeParser.StructSizeValue.Select(s => new StructSizeof(((CompileTimeStructSize)s).StructName)))
            .Or(StructMember)
            .Or(VariableToken);

    private static readonly Parser<IValueExpression> Postfix =
        from primary in Parse.Ref(() => Unary)
        from calls in CallSuffix.Many()
            .Concat(IndexSuffix.Many())
        select calls.Aggregate(primary, (expr, call) => call(expr));
    
    // Argument list parser (supporting 0 or more)
    private static readonly Parser<IEnumerable<IValueExpression>> ArgumentList =
        from lparen in Parse.Char('(').Token()
        from args in Expression.DelimitedBy(Parse.Char(',').Token()).Optional()
        from rparen in Parse.Char(')').Token()
        select args.GetOrElse([]);
    
    // Suffix parser returns a function that "calls" the prior expr with args
    private static readonly Parser<Func<IValueExpression, IValueExpression>> CallSuffix =
        from args in ArgumentList
        select (Func<IValueExpression, IValueExpression>)(target =>
            new FunctionCall(target, args.ToArray()));
    
    // Suffix parser for array indexing, eg. varName[expr]
    // optionally, varName[expr,size] where the index becomes Binop multiply expr by size
    // should transform 
    // varName[expr,size]  =>  (varName + (expr UNSIGNED_MULTIPLY size))
    private static readonly Parser<Func<IValueExpression, IValueExpression>> IndexSuffix =
        from lbracket in Parse.Char('[').Token()
        from indexExpr in Expression
        from sizeExpr in
            (from comma in Parse.Char(',').Token()
             from size in Expression
             select size).Optional()
        from rbracket in Parse.Char(']').Token()
        select (Func<IValueExpression, IValueExpression>)(target => {
            if (sizeExpr.IsDefined) {
                BinaryOperation multiply = new(indexExpr, BinaryOperationType.UnsignedMultiply, sizeExpr.Get());
                BinaryOperation addition = new(target, BinaryOperationType.Add, multiply);
                return addition;
            } else {
                BinaryOperation addition = new(target, BinaryOperationType.Add, indexExpr);
                return addition;
            }
        });

    // Parser helpers for operators
    private static Parser<string> Op(string op) => Parse.String(op).Token().Text();
    private static Parser<string> Op(params string[] ops) {
        Parser<IEnumerable<char>>? p = Parse.String(ops[0]).Token();
        for (int i = 1; i < ops.Length; i++) {
            p = p.Or(Parse.String(ops[i]).Token());
        }
        return p.Text();
    }
    
    private static readonly Parser<Func<IValueExpression, IValueExpression>> UnaryOp =
        Op("~", "-", "!")
            .Select(op => (Func<IValueExpression, IValueExpression>)(expr => new UnaryOperation(
                op switch {
                    "-" => UnaryOperationType.Negate,
                    "~" => UnaryOperationType.BitwiseNot,
                    "!" => UnaryOperationType.LogicalNot,
                    _ => throw new InvalidOperationException()
                }, expr)));
    
    private static readonly Parser<IValueExpression> Unary =
        from ops in UnaryOp.Many()
        from expr in Atom
        select ops
            .Reverse()
            .Aggregate(expr, (acc, makeOp) => makeOp(acc));

    // Operator precedence (highest first!):
    
    // 0. A dereference, eg. myVar:4 (gets first 4 bytes at myVar)
    // supports any CompileTimeValue as size
    public static readonly Parser<IValueExpression> Dereference =
        Parse.ChainOperator(
            Op(":"), 
            Postfix,
            (_, left, right) => new BinaryOperation(left, BinaryOperationType.Dereference, right));
    
    // 1. *, /, %
    private static readonly Parser<IValueExpression> MulDivMod =
        Parse.ChainOperator(
            Op("*", "/", "~*", "~/", "%", "~%"),
            Dereference,
            (op, left, right) => new BinaryOperation(left, op switch {
                "*" => BinaryOperationType.UnsignedMultiply,
                "/" => BinaryOperationType.UnsignedDivide,
                "~*" => BinaryOperationType.SignedMultiply,
                "~/"=> BinaryOperationType.SignedDivide,
                "%" => BinaryOperationType.UnsignedModulus,
                "~%" => BinaryOperationType.SignedModulus,
                _ => throw new InvalidOperationException()
            }, right));

    // 2. +, -
    private static readonly Parser<IValueExpression> AddSub =
        Parse.ChainOperator(
            Op("+", "-"),
            MulDivMod,
            (op, left, right) => new BinaryOperation(left, op switch {
                    "+" => BinaryOperationType.Add,
                    "-" => BinaryOperationType.Subtract,
                    _ => throw new InvalidOperationException()
                }, right));

    // 3. <<, >>
    private static readonly Parser<IValueExpression> Shifts =
        Parse.ChainOperator(
            Op("<<", ">>"),
            AddSub,
            (op, left, right) => new BinaryOperation(left, 
                op == "<<" ? BinaryOperationType.LeftShift : BinaryOperationType.RightShift, right));

    // 4. &
    private static readonly Parser<IValueExpression> BitAnd =
        Parse.ChainOperator(
            Op("&"),
            Shifts,
            (_, left, right) => new BinaryOperation(left, BinaryOperationType.BitwiseAnd, right));

    // 5. ^
    private static readonly Parser<IValueExpression> BitXor =
        Parse.ChainOperator(
            Op("^"),
            BitAnd,
            (_, left, right) => new BinaryOperation(left, BinaryOperationType.BitwiseXor, right));

    // 6. |
    private static readonly Parser<IValueExpression> BitOr =
        Parse.ChainOperator(
            Op("|"),
            BitXor,
            (_, left, right) => new BinaryOperation(left, BinaryOperationType.BitwiseOr, right));

    // 7. ==, !=, <, <=, >, >=
    private static readonly Parser<IValueExpression> Comparison =
        Parse.ChainOperator(
            Op("==", "!=", "<", "<=", ">", ">=", "~<", "~>", "~<=","~>="),
            BitOr,
            (op, left, right) => new BinaryOperation(left, op switch {
                "==" => BinaryOperationType.Equals,
                "!=" => BinaryOperationType.NotEquals,
                "<" => BinaryOperationType.UnsignedLessThan,
                "<=" => BinaryOperationType.UnsignedLessThanOrEqual,
                "~<" => BinaryOperationType.SignedLessThan,
                "~<=" => BinaryOperationType.SignedLessThanOrEqual,
                ">" => BinaryOperationType.UnsignedGreaterThan,
                ">=" => BinaryOperationType.UnsignedGreaterThanOrEqual,
                "~>" => BinaryOperationType.SignedGreaterThan,
                "~>=" => BinaryOperationType.SignedGreaterThanOrEqual,
                _ => throw new InvalidOperationException()
            }, right));
    
    // Top-level parser entry
    public static readonly Parser<IValueExpression> Expression = StringLiteral.Or(Comparison);
}
