using Sprache;

namespace IntegerMaths;

public static class MathsParser {
    public static readonly Parser<Expr> Number =
        // Signed number: optional leading '+' or '-'
        (from sign in Parse.Char('+').Or(Parse.Char('-')).Optional()
         from number in UnsignedNumber
         select sign.IsDefined && sign.Get() == '-'
             ? new Binary(new Literal(0), "-", number)
             : number)
        .Token();
    
    // Parses both hex ("0x1A2B") and decimal ("1234") numbers
    public static readonly Parser<Expr> UnsignedNumber =
        // Hex
        (from prefix in Parse.String("0x").Or(Parse.String("0X"))
         from hex in Parse.Chars("0123456789abcdefABCDEF_").AtLeastOnce().Text()
         select (Expr)new Literal(Convert.ToUInt64(hex.Replace("_", ""), 16)))
        // Octal
        .Or(
            from prefix in Parse.String("0o").Or(Parse.String("0O"))
            from oct in Parse.Chars("01234567_").AtLeastOnce().Text()
            select (Expr)new Literal(Convert.ToUInt64(oct.Replace("_", ""), 8))
        )
        // Binary
        .Or(
            from prefix in Parse.String("0b").Or(Parse.String("0B"))
            from bin in Parse.Chars("01_").AtLeastOnce().Text()
            select (Expr)new Literal(Convert.ToUInt64(bin.Replace("_", ""), 2))
        )
        // Decimal
        .Or(
            from first in Parse.Chars("0123456789").Once().Text()  // can't start with underscore
            from rest in Parse.Chars("0123456789_").Many().Text()
            let dec = first + rest
            select (Expr)new Literal(Convert.ToUInt64(dec.Replace("_", ""), 10))
        )
        // Char ('A' or '\n')
        .Or(
            from openQuote in Parse.Char('\'')
            from content in 
                // Handle escaped char: e.g. '\n', '\''
                Parse.Char('\\').Then(_ => Parse.AnyChar.Select(escaped => escaped switch {
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
                        _ => escaped // Unknown escapes just literal
                    }))
                    .Or(Parse.CharExcept(c => c is '\'' or '\\', "")) // Normal character except quote and backslash
            from closeQuote in Parse.Char('\'')
            select (Expr)new Literal((ulong)content)
        )
        // Variables (identifiers)
        .Or(
            from ident in Parse.Letter.Or(Parse.Char('_'))
                .Then(first => Parse.LetterOrDigit.Or(Parse.Char('_')).Many()
                    .Select(rest => first + new string(rest.ToArray())))
            select (Expr)new Variable(ident)
        )
        .Token();

    // Parentheses expression
    private static readonly Parser<Expr> Parens =
        from lparen in Parse.Char('(').Token()
        from expr in Parse.Ref(() => Expression)
        from rparen in Parse.Char(')').Token()
        select expr;

    // Atom: either a number or a parenthesized subexpression
    private static readonly Parser<Expr> Atom =
        Parens.Or(Number);

    // Parser helpers for operators
    private static Parser<string> Op(string op) => Parse.String(op).Token().Text();
    private static Parser<string> Op(params string[] ops) {
        Parser<IEnumerable<char>>? p = Parse.String(ops[0]).Token();
        for (int i = 1; i < ops.Length; i++) {
            p = p.Or(Parse.String(ops[i]).Token());
        }
        return p.Text();
    }

    // Operator precedence (highest first!):
    // 1. *, /, %
    private static readonly Parser<Expr> MulDivMod =
        Parse.ChainOperator(
            Op("*", "/", "%"),
            Atom,
            (op, left, right) => new Binary(left, op, right));

    // 2. +, -
    private static readonly Parser<Expr> AddSub =
        Parse.ChainOperator(
            Op("+", "-"),
            MulDivMod,
            (op, left, right) => new Binary(left, op, right));

    // 3. <<, >>
    private static readonly Parser<Expr> Shifts =
        Parse.ChainOperator(
            Op("<<", ">>"),
            AddSub,
            (op, left, right) => new Binary(left, op, right));

    // 4. &
    private static readonly Parser<Expr> BitAnd =
        Parse.ChainOperator(
            Op("&"),
            Shifts,
            (op, left, right) => new Binary(left, op, right));

    // 5. ^
    private static readonly Parser<Expr> BitXor =
        Parse.ChainOperator(
            Op("^"),
            BitAnd,
            (op, left, right) => new Binary(left, op, right));

    // 6. |
    private static readonly Parser<Expr> BitOr =
        Parse.ChainOperator(
            Op("|"),
            BitXor,
            (op, left, right) => new Binary(left, op, right));

    // Top-level parser entry
    // Call .End() when using this.
    public static readonly Parser<Expr> Expression = BitOr;
}
