namespace CatVM.Testing.Ops;

public class AndOperationTest : OperationTestBase {

    [Test]
    public void TestAndRR() {
        _vm.Cpu.R4 = 0b1010;
        _vm.Cpu.R5 = 0b0111;
        Execute(0x2b, 0x04, 0x05);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(0b0010));
    }
    
    [Test]
    public void TestAndRI() {
        _vm.Cpu.R4 = 0b1010;
        Execute(0x2c, 0x04, 0b0111, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(0b0010));
    }

    [Test]
    public void TestAndDoesNotTouchFlags() {
        _vm.Cpu.R4 = 0;
        _vm.Cpu.R5 = 0;
        // Pre-set flags to opposite of what an arithmetic-style impl would set,
        // and verify they survive.
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.CarryFlag = true;
        _vm.Cpu.SignFlag = true;
        _vm.Cpu.OverflowFlag = true;
        Execute(0x2b, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(0u));
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.CarryFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.True);
            Assert.That(_vm.Cpu.OverflowFlag, Is.True);
        });
    }

    [Test]
    public void TestAndAllOnes_IsIdentity() {
        _vm.Cpu.R4 = 0xDEADBEEF;
        Execute(0x2c, 0x04, 0xFF, 0xFF, 0xFF, 0xFF);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(0xDEADBEEFu));
    }
}
