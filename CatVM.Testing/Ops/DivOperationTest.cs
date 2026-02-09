namespace CatVM.Testing.Ops;

public class DivOperationTest : OperationTestBase {
    
    [Test]
    public void TestUDivRR() {
        _vm.Cpu.R4 = 10;
        _vm.Cpu.R5 = 5;
        Execute(0x1c, 0x04, 0x05);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(2));
    }
    
    [Test]
    public void TestIDivRR() {
        _vm.Cpu.R4 = 10;
        _vm.Cpu.R5 = uint.MaxValue - 4;  // -5 in two's complement
        Execute(0x1d, 0x04, 0x05);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(uint.MaxValue - 1));
    }
}
