namespace CatVM.Testing.Ops;

public class SubOperationTest : OperationTestBase {

    [Test]
    public void TestSubRR() {
        _vm.Cpu.R4 = 15;
        _vm.Cpu.R5 = 5;
        Execute(0x16, 0x04, 0x05);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(10));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestSubRI() {
        _vm.Cpu.R4 = 15;
        Execute(0x17, 0x04, 0x0A, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(5));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestSubZeroFlag() {
        _vm.Cpu.R4 = 5;
        _vm.Cpu.R5 = 5;
        Execute(0x16, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(0u));
            Assert.That(_vm.Cpu.ZeroFlag, Is.True);
            Assert.That(_vm.Cpu.CarryFlag, Is.False);
            Assert.That(_vm.Cpu.SignFlag, Is.False);
            Assert.That(_vm.Cpu.OverflowFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }

    [Test]
    public void TestSubBorrow() {
        // 0 - 1 = 0xFFFFFFFF (carry/borrow set)
        _vm.Cpu.R4 = 0;
        _vm.Cpu.R5 = 1;
        Execute(0x16, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(0xFFFFFFFFu));
            Assert.That(_vm.Cpu.CarryFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.True);
            Assert.That(_vm.Cpu.OverflowFlag, Is.False);
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }

    [Test]
    public void TestSubSignedOverflow_IntMinMinusOne() {
        // INT_MIN - 1 = INT_MAX (signed overflow)
        _vm.Cpu.R4 = 0x80000000;
        Execute(0x17, 0x04, 0x01, 0x00, 0x00, 0x00);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(0x7FFFFFFFu));
            Assert.That(_vm.Cpu.OverflowFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.False);
            Assert.That(_vm.Cpu.CarryFlag, Is.False);
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
        });
    }

    [Test]
    public void TestSubSignedOverflow_IntMaxMinusNegOne() {
        // INT_MAX - (-1) = INT_MIN (signed overflow); 0x7FFFFFFF - 0xFFFFFFFF = 0x80000000, carry set
        _vm.Cpu.R4 = 0x7FFFFFFF;
        _vm.Cpu.R5 = 0xFFFFFFFF;
        Execute(0x16, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(0x80000000u));
            Assert.That(_vm.Cpu.OverflowFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.True);
            Assert.That(_vm.Cpu.CarryFlag, Is.True);
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }
}
