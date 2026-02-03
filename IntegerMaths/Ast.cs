using System.Numerics;

namespace IntegerMaths;

public abstract record Expr {
    public BigInteger LiteralValue() {
        if (this is Literal l) {
            return l.Value;
        }
        throw new InvalidOperationException($"Not a literal: {GetType().Name}");
    }
}
public record Literal(BigInteger Value) : Expr;
public record Variable(string Name) : Expr;
public record Binary(Expr Left, string Op, Expr Right) : Expr;
