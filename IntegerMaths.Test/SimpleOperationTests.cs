namespace IntegerMaths.Test;

public class SimpleOperationTests {
    
    [Test]
    public void SimpleAddition() {
        Expression expr = new("1 + 2");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(3));
    }

    [Test]
    public void SimpleSubtraction() {
        Expression expr = new("5 - 2");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(3));
    }
    
    [Test]
    public void SimpleMultiplication() {
        Expression expr = new("3 * 4");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(12));
    }

    [Test]
    public void SimpleDivision() {
        Expression expr = new("8 / 2");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(4));
    }

    [Test]
    public void SimpleModulo() {
        Expression expr = new("65 % 3");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(2));
    }

    [Test]
    public void SimpleBitwiseAnd() {
        Expression expr = new("6 & 3");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(2));
    }
    
    [Test]
    public void SimpleBitwiseOr() {
        Expression expr = new("6 | 3");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(7));
    }

    [Test]
    public void SimpleBitwiseXor() {
        Expression expr = new("6 ^ 3");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(5));
    }
    
    [Test]
    public void SimpleLeftShift() {
        Expression expr = new("3 << 2");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(12));
    }
    
    [Test]
    public void SimpleRightShift() {
        Expression expr = new("12 >> 2");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(3));
    }

    [Test]
    public void NegativeLiteral() {
        Expression expr = new("-5");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(uint.MaxValue - 4));
    }
}
