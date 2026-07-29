namespace CatVM.Testing.Ops;

public class NotOperationTest : OperationTestBase {
    
    [Test]
    public void TestNotOperation() {
        _vm.Cpu.R1 = 0b11111111_11111111_11111111_10101010;
        Execute(0x2f, 0x01);  // NOT R1
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b01010101));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(2u));
    }

    [Test]
    public void TestNotZero_GivesAllOnes() {
        _vm.Cpu.R1 = 0;
        Execute(0x2f, 0x01);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0xFFFFFFFFu));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(2u));
    }

    [Test]
    public void TestNotDoesNotTouchFlags() {
        _vm.Cpu.R1 = 0;
        _vm.Cpu.ZeroFlag = true;
        _vm.Cpu.CarryFlag = true;
        _vm.Cpu.SignFlag = false;
        _vm.Cpu.OverflowFlag = true;
        Execute(0x2f, 0x01);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.ZeroFlag, Is.True);
            Assert.That(_vm.Cpu.CarryFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.False);
            Assert.That(_vm.Cpu.OverflowFlag, Is.True);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(2u));
        });
    }

    [Test]
    public void TestNotInvolution() {
        _vm.Cpu.R1 = 0xDEADBEEF;
        Execute(0x2f, 0x01);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(~0xDEADBEEFu));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(2u));
        Execute(0x2f, 0x01);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0xDEADBEEFu));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(2u));
    }
}
