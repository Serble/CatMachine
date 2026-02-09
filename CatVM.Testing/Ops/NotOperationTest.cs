namespace CatVM.Testing.Ops;

public class NotOperationTest : OperationTestBase {
    
    [Test]
    public void TestNotOperation() {
        _vm.Cpu.R1 = 0b11111111_11111111_11111111_10101010;
        Execute(0x2f, 0x01);  // NOT R1
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0b01010101));
    }
}
