namespace CatVM.Testing.Ops;

public class OrOperationTest : OperationTestBase {
    
    [Test]
    public void TestOrRR() {
        _vm.Cpu.R1 = 0b10101010;
        _vm.Cpu.R2 = 0b01010101;
        Execute(0x29, 0x01, 0x02);  // OR R1, R2
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b11111111));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestOrRI() {
        _vm.Cpu.R1 = 0b10101010;
        Execute(0x2a, 0x01, 0b01010101, 0x00, 0x00, 0x00);  // OR R1, 0b01010101
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b11111111));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestOrDoesNotTouchFlags() {
        _vm.Cpu.R1 = 0;
        _vm.Cpu.R2 = 0;
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.CarryFlag = true;
        _vm.Cpu.SignFlag = true;
        _vm.Cpu.OverflowFlag = true;
        Execute(0x29, 0x01, 0x02);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R1, Is.EqualTo(0u));
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.CarryFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.True);
            Assert.That(_vm.Cpu.OverflowFlag, Is.True);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }

    [Test]
    public void TestOrZero_IsIdentity() {
        _vm.Cpu.R1 = 0xDEADBEEF;
        Execute(0x2a, 0x01, 0x00, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0xDEADBEEFu));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }
}
