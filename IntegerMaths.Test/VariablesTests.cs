namespace IntegerMaths.Test;

public class VariablesTests {
    
    [Test]
    public void VariableAddition() {
        Expression expr = new("a + b");
        expr.Variables.Add("a", "5");
        expr.Variables.Add("b", "10");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(15));
    }

    [Test]
    public void VariableMultiplication() {
        Expression expr = new("x * y");
        expr.Variables.Add("x", "7");
        expr.Variables.Add("y", "6");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(42));
    }
    
    [Test]
    public void NestedVariableExpression() {
        Expression expr = new("m + n");
        expr.Variables.Add("m", "p * 2");
        expr.Variables.Add("n", "3");
        expr.Variables.Add("p", "4");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(11)); // (4 * 2) + 3 = 11
    }
    
    [Test]
    public void VariableWithDifferentBases() {
        Expression expr = new("var1 + var2 + var3 + var4");
        expr.Variables.Add("var1", "42");        // Decimal
        expr.Variables.Add("var2", "0x2A");      // Hexadecimal
        expr.Variables.Add("var3", "0b101010");  // Binary
        expr.Variables.Add("var4", "0o52");      // Octal
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(42 + 0x2A + 0b101010 + 42));
    }

    [Test]
    public void EventEvaluation() {
        Expression expr = new("a + b");
        expr.EvaluateVariableEvent += (name, args) => {
            args.Value = name switch {
                "a" => 7,
                "b" => 8,
                _ => null
            };
        };
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(15));
    }
}