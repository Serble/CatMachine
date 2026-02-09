namespace CatVM.Testing.Ops;

public class NopOperationTest : OperationTestBase {
    
    [Test]
    public void TestNop() {
        CatCpuState original = _vm.Cpu;
        Execute(0x4d);  // NOP
        original.Ip = _vm.Cpu.Ip;  // IP should be the only thing changed
        Assert.That(_vm.Cpu, Is.EqualTo(original));
    }
}
