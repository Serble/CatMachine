namespace CatVM.Testing.Ops;

public class AddOperationTest : OperationTestBase {

    [Test]
    public void TestAddRR() {
        _vm.Cpu.R4 = 5;
        _vm.Cpu.R5 = 10;
        Execute(0x14, 0x04, 0x05);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(15));
    }
    
    [Test]
    public void TestAddRI() {
        _vm.Cpu.R4 = 5;
        Execute(0x15, 0x04, 0x0A, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(15));
    }
}
