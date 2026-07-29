namespace CatVM.Testing.Ops;

public class XorOperationTest : OperationTestBase {

    [Test]
    public void TestXorRR() {
        _vm.Cpu.R1 = 0b10101010;
        _vm.Cpu.R2 = 0b11001100;
        Execute(0x2d, 0x01, 0x02);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b10101010 ^ 0b11001100));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestXorRI() {
        _vm.Cpu.R1 = 0b10101010;
        Execute(0x2e, 0x01, 0b11001100, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b10101010 ^ 0b11001100));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestXorSelfIsZero() {
        _vm.Cpu.R1 = 0xDEADBEEF;
        Execute(0x2d, 0x01, 0x01);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0u));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }

    [Test]
    public void TestXorDoesNotTouchFlags() {
        _vm.Cpu.R1 = 0;
        _vm.Cpu.R2 = 0;
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.CarryFlag = true;
        _vm.Cpu.SignFlag = true;
        _vm.Cpu.OverflowFlag = true;
        Execute(0x2d, 0x01, 0x02);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.CarryFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.True);
            Assert.That(_vm.Cpu.OverflowFlag, Is.True);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }
}
