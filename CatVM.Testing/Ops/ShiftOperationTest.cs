namespace CatVM.Testing.Ops;

public class ShiftOperationTest : OperationTestBase {
    
    [Test]
    public void TestShlRR() {
        _vm.Cpu.R1 = 0b00000000_00000000_00000000_10101010;
        _vm.Cpu.R2 = 3;
        Execute(0x4e, 0x01, 0x02);  // SHL R1, R2
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b00000000_00000000_00000101_01010000));
    }
    
    [Test]
    public void TestShlRI() {
        _vm.Cpu.R1 = 0b00000000_00000000_00000000_10101010;
        Execute(0x4f, 0x01, 0x03, 0x00, 0x00, 0x00);  // SHL R1, 3
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b00000000_00000000_00000101_01010000));
    }
    
    [Test]
    public void TestShrRR() {
        _vm.Cpu.R1 = 0b00000000_00000000_00000000_10101010;
        _vm.Cpu.R2 = 3;
        Execute(0x50, 0x01, 0x02);  // SHR R1, R2
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b00000000_00000000_00000000_00010101));
    }
    
    [Test]
    public void TestShrRI() {
        _vm.Cpu.R1 = 0b00000000_00000000_00000000_10101010;
        Execute(0x51, 0x01, 0x03, 0x00, 0x00, 0x00);  // SHR R1, 3
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b00000000_00000000_00000000_00010101));
    }
}
