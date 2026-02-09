namespace CatVM.Testing.Ops;

public class OrOperationTest : OperationTestBase {
    
    [Test]
    public void TestOrRR() {
        _vm.Cpu.R1 = 0b10101010;
        _vm.Cpu.R2 = 0b01010101;
        Execute(0x29, 0x01, 0x02);  // OR R1, R2
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b11111111));
    }
    
    [Test]
    public void TestOrRI() {
        _vm.Cpu.R1 = 0b10101010;
        Execute(0x2a, 0x01, 0b01010101, 0x00, 0x00, 0x00);  // OR R1, 0b01010101
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b11111111));
    }
}
