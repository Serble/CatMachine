namespace CatVM.Testing.Ops;

public class AddOperationTest : OperationTestBase {

    [Test]
    public void TestAddRR() {
        _vm.Cpu.R4 = 5;
        _vm.Cpu.R5 = 10;
        Execute(0x14, 0x04, 0x05);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(15));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestAddRI() {
        _vm.Cpu.R4 = 5;
        Execute(0x15, 0x04, 0x0A, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(15));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestAddZeroFlag() {
        _vm.Cpu.R4 = 0;
        _vm.Cpu.R5 = 0;
        Execute(0x14, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(0u));
            Assert.That(_vm.Cpu.ZeroFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.False);
            Assert.That(_vm.Cpu.CarryFlag, Is.False);
            Assert.That(_vm.Cpu.OverflowFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }

    [Test]
    public void TestAddSignFlag() {
        // 1 + 0x7FFFFFFF = 0x80000000 → negative result, signed overflow
        _vm.Cpu.R4 = 1;
        _vm.Cpu.R5 = 0x7FFFFFFF;
        Execute(0x14, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(0x80000000u));
            Assert.That(_vm.Cpu.SignFlag, Is.True);
            Assert.That(_vm.Cpu.OverflowFlag, Is.True);
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.CarryFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }

    [Test]
    public void TestAddCarryFlag() {
        // 0xFFFFFFFF + 1 = 0 with carry
        _vm.Cpu.R4 = 0xFFFFFFFF;
        _vm.Cpu.R5 = 1;
        Execute(0x14, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(0u));
            Assert.That(_vm.Cpu.CarryFlag, Is.True);
            Assert.That(_vm.Cpu.ZeroFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.False);
            Assert.That(_vm.Cpu.OverflowFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }

    [Test]
    public void TestAddSignedOverflow() {
        // INT_MAX + 1 = INT_MIN (signed overflow, no carry)
        _vm.Cpu.R4 = 0x7FFFFFFF;
        Execute(0x15, 0x04, 0x01, 0x00, 0x00, 0x00);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(0x80000000u));
            Assert.That(_vm.Cpu.OverflowFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.True);
            Assert.That(_vm.Cpu.CarryFlag, Is.False);
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
        });
    }

    [Test]
    public void TestAddTwoNegatives_OverflowToPositive() {
        // (-1) + (-1) = -2 (no overflow, both signed): result = 0xFFFFFFFE, carry set
        _vm.Cpu.R4 = 0xFFFFFFFF;
        _vm.Cpu.R5 = 0xFFFFFFFF;
        Execute(0x14, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(0xFFFFFFFEu));
            Assert.That(_vm.Cpu.SignFlag, Is.True);
            Assert.That(_vm.Cpu.CarryFlag, Is.True);
            Assert.That(_vm.Cpu.OverflowFlag, Is.False);
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }
}
