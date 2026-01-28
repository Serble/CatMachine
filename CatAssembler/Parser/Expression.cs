using CatData;

namespace CatAssembler.Parser;

public interface IExpression {
    string RawValue { get; }
}

public interface IPointerCapableExpression : IExpression {
    bool Pointer { get; init; }
}

public record NumberExpression(string RawValue, string Value, bool Pointer) : IPointerCapableExpression;

public record RegisterExpression(string RawValue, Register Value, bool Pointer) : IPointerCapableExpression;

public record NameExpression(string RawValue, string Value) : IExpression {
    public NumberExpression ToNumber() => new(RawValue, Value, false);
}

public record StringExpression(string RawValue, string Value) : IExpression;

public record MacroBodyExpression(List<string> Value, int LineNumber) : IExpression {
    public string RawValue => string.Join('\n', Value);
}
