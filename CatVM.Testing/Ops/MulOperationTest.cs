namespace CatVM.Testing.Ops;

public class MulOperationTest : OperationTestBase {
    
    [Test]
    public void TestUMulRR() {
        _vm.Cpu.R1 = 5;
        _vm.Cpu.R2 = 10;
        Execute(0x18, 0x01, 0x02);  // UMUL R1, R2
        Assert.That(_vm.Cpu.R1, Is.EqualTo(50));
    }
    
    [Test]
    public void TestUMulRI() {
        _vm.Cpu.R1 = 5;
        Execute(0x19, 0x01, 0x0A, 0x00, 0x00, 0x00);  // UMUL R1, 10
        Assert.That(_vm.Cpu.R1, Is.EqualTo(50));
    }
    
    [Test]
    public void TestIMulRR() {
        _vm.Cpu.R1 = uint.MaxValue - 4;  // -5
        _vm.Cpu.R2 = 10;                // 10
        Execute(0x1A, 0x01, 0x02);  // IMUL R1, R2
        Assert.That(_vm.Cpu.R1, Is.EqualTo(uint.MaxValue - 49));  // -50
    }
    
    [Test]
    public void TestIMulRI() {
        _vm.Cpu.R1 = uint.MaxValue - 4;  // -5
        Execute(0x1B, 0x01, 0x0A, 0x00, 0x00, 0x00);  // IMUL R1, 10
        Assert.That(_vm.Cpu.R1, Is.EqualTo(uint.MaxValue - 49));  // -50
    }

    [Test]
    public void TestUMulOverflowWraps() {
        // 0xFFFFFFFF * 2 = 0xFFFFFFFE (wrapping); behaviour pinned (no flags currently set)
        _vm.Cpu.R1 = 0xFFFFFFFF;
        _vm.Cpu.R2 = 2;
        bool zBefore = _vm.Cpu.ZeroFlag;
        bool cBefore = _vm.Cpu.CarryFlag;
        bool sBefore = _vm.Cpu.SignFlag;
        bool oBefore = _vm.Cpu.OverflowFlag;
        Execute(0x18, 0x01, 0x02);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R1, Is.EqualTo(0xFFFFFFFEu));
            // Mul does not currently update flags — pin that behaviour.
            Assert.That(_vm.Cpu.ZeroFlag, Is.EqualTo(zBefore));
            Assert.That(_vm.Cpu.CarryFlag, Is.EqualTo(cBefore));
            Assert.That(_vm.Cpu.SignFlag, Is.EqualTo(sBefore));
            Assert.That(_vm.Cpu.OverflowFlag, Is.EqualTo(oBefore));
        });
    }

    [Test]
    public void TestIMulOverflowWraps() {
        // INT_MIN * -1 wraps back to INT_MIN.
        _vm.Cpu.R1 = 0x80000000;          // INT_MIN
        _vm.Cpu.R2 = 0xFFFFFFFF;          // -1
        Execute(0x1A, 0x01, 0x02);        // IMUL R1, R2
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0x80000000u));
    }

    [Test]
    public void TestUMulByZero() {
        _vm.Cpu.R1 = 0xCAFEBABE;
        Execute(0x19, 0x01, 0x00, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0u));
    }
}
