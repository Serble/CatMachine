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
}
