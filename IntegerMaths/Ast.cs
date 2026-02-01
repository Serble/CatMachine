using System.Numerics;

namespace IntegerMaths;

internal abstract record Expr;
internal record Literal(BigInteger Value) : Expr;
internal record Variable(string Name) : Expr;
internal record Binary(Expr Left, string Op, Expr Right) : Expr;
