namespace CatVM.Testing.Ops;

public class SubOperationTest : OperationTestBase {

    [Test]
    public void TestSubRR() {
        _vm.Cpu.R4 = 15;
        _vm.Cpu.R5 = 5;
        Execute(0x16, 0x04, 0x05);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(10));
    }
    
    [Test]
    public void TestSubRI() {
        _vm.Cpu.R4 = 15;
        Execute(0x17, 0x04, 0x0A, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(5));
    }
}
