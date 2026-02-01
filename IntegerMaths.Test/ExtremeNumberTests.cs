namespace IntegerMaths.Test;

public class ExtremeNumberTests {

    [Test]
    public void LargeAddition() {
        Expression expr = new("0xFFFFFF00 + 1");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(0xFFFFFF01));
    }

    [Test]
    public void LargeMultiplication() {
        Expression expr = new("0xFFFF * 0xFFFF");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(0xFFFE0001));
    }

    [Test]
    public void ConstantUnderflow() {
        Expression expr = new("-1");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(0xFFFFFFFF));
    }
    
    [Test]
    public void ConstantOverflow() {
        Expression expr = new("0x100000000");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(0));
    }
}
