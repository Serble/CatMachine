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
}
