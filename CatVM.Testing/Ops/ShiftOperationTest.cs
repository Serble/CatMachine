namespace CatVM.Testing.Ops;

public class ShiftOperationTest : OperationTestBase {
    
    [Test]
    public void TestShlRR() {
        _vm.Cpu.R1 = 0b00000000_00000000_00000000_10101010;
        _vm.Cpu.R2 = 3;
        Execute(0x4e, 0x01, 0x02);  // SHL R1, R2
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b00000000_00000000_00000101_01010000));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestShlRI() {
        _vm.Cpu.R1 = 0b00000000_00000000_00000000_10101010;
        Execute(0x4f, 0x01, 0x03, 0x00, 0x00, 0x00);  // SHL R1, 3
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b00000000_00000000_00000101_01010000));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }
    
    [Test]
    public void TestShrRR() {
        _vm.Cpu.R1 = 0b00000000_00000000_00000000_10101010;
        _vm.Cpu.R2 = 3;
        Execute(0x50, 0x01, 0x02);  // SHR R1, R2
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b00000000_00000000_00000000_00010101));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestShrRI() {
        _vm.Cpu.R1 = 0b00000000_00000000_00000000_10101010;
        Execute(0x51, 0x01, 0x03, 0x00, 0x00, 0x00);  // SHR R1, 3
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b00000000_00000000_00000000_00010101));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestShlByZero_IsIdentity() {
        _vm.Cpu.R1 = 0xDEADBEEF;
        Execute(0x4f, 0x01, 0x00, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0xDEADBEEFu));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestShrByZero_IsIdentity() {
        _vm.Cpu.R1 = 0xDEADBEEF;
        Execute(0x51, 0x01, 0x00, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0xDEADBEEFu));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestShlBy31() {
        _vm.Cpu.R1 = 1;
        Execute(0x4f, 0x01, 31, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0x80000000u));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestShrBy31() {
        // SHR is logical (zero-extending) since impl uses uint
        _vm.Cpu.R1 = 0x80000000;
        Execute(0x51, 0x01, 31, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(1u));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestShl_ShiftAmount32_MasksTo0_PinningBehavior() {
        // C# masks the shift count to the low 5 bits for uint, so shift by 32 == shift by 0.
        _vm.Cpu.R1 = 0xDEADBEEF;
        Execute(0x4f, 0x01, 32, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0xDEADBEEFu));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestShr_LogicalNotArithmetic() {
        // SHR of negative-looking value should zero-fill (logical), not sign-extend.
        _vm.Cpu.R1 = 0xFFFFFFFF;
        Execute(0x51, 0x01, 1, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0x7FFFFFFFu));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestShiftDoesNotTouchFlags() {
        _vm.Cpu.R1 = 1;
        _vm.Cpu.ZeroFlag = true;
        _vm.Cpu.CarryFlag = false;
        _vm.Cpu.SignFlag = true;
        _vm.Cpu.OverflowFlag = false;
        Execute(0x4f, 0x01, 31, 0x00, 0x00, 0x00);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.ZeroFlag, Is.True);
            Assert.That(_vm.Cpu.CarryFlag, Is.False);
            Assert.That(_vm.Cpu.SignFlag, Is.True);
            Assert.That(_vm.Cpu.OverflowFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
        });
    }
}
