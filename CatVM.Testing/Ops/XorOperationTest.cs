namespace CatVM.Testing.Ops;

public class XorOperationTest : OperationTestBase {

    [Test]
    public void TestXorRR() {
        _vm.Cpu.R1 = 0b10101010;
        _vm.Cpu.R2 = 0b11001100;
        Execute(0x2d, 0x01, 0x02);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b10101010 ^ 0b11001100));
    }
    
    [Test]
    public void TestXorRI() {
        _vm.Cpu.R1 = 0b10101010;
        Execute(0x2e, 0x01, 0b11001100, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b10101010 ^ 0b11001100));
    }
}
