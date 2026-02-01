namespace IntegerMaths.Test;

public class OrderOfOperationsTests {
    
    [Test]
    public void SimpleBodmas() {
        Expression expr = new("1 + 2 * 3");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(7));
    }
    
    [Test]
    public void ParenthesesBodmas() {
        Expression expr = new("(1 + 2) * 3");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(9));
    }

    [Test]
    public void MixedOperations() {
        Expression expr = new("4 + 2 * 3 - 8 / 4");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(4 + 2 * 3 - 8 / 4));
    }

    [Test]
    public void BitwiseAndOrPrecedence() {
        Expression expr = new("5 | 3 & 2");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(5 | (3 & 2)));
    }
}
