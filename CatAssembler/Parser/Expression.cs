namespace CatAssembler.Parser;

public interface IExpression;

public interface IPointerCapableExpression : IExpression {
    bool Pointer { get; init; }
}

public record NumberExpression(string Value, bool Pointer) : IPointerCapableExpression;

public record RegisterExpression(Register Value, bool Pointer) : IPointerCapableExpression;

public record NameExpression(string Value) : IExpression {
    public NumberExpression ToNumber() => new(Value, false);
}

public record StringExpression(string Value) : IExpression;
