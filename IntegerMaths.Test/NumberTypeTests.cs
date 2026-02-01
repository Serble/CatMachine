namespace IntegerMaths.Test;

public class NumberTypeTests {
    
    [Test]
    public void DecimalNumber() {
        Expression expr = new("42");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(42));
    }

    [Test]
    public void HexadecimalNumber() {
        Expression expr = new("0x2A");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(0x2A));
    }
    
    [Test]
    public void BinaryNumber() {
        Expression expr = new("0b101010");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(0b101010));
    }

    [Test]
    public void OctalNumber() {
        Expression expr = new("0o52");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(42));
    }
    
    [Test]
    public void SpacedDecimalNumber() {
        Expression expr = new("1_000_000");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(1000000));
    }
    
    [Test]
    public void SpacedHexadecimalNumber() {
        Expression expr = new("0xFF_FF_FF_FF");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(0xFFFFFFFF));
    }
    
    [Test]
    public void SpacedBinaryNumber() {
        Expression expr = new("0b1111_1111_1111_1111");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(0xFFFF));
    }
    
    [Test]
    public void SpacedOctalNumber() {
        Expression expr = new("0o12_34_56_70");
        uint result = expr.EvalAsUInt();
        Assert.That(result, Is.EqualTo(2739128));
    }
}
