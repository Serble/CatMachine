namespace CatVM.Testing.Ops;

public class NopOperationTest : OperationTestBase {
    
    [Test]
    public void TestNop() {
        CatCpuState original = _vm.Cpu;
        Execute(0x4d);  // NOP
        original.Ip = _vm.Cpu.Ip;  // IP should be the only thing changed
        Assert.That(_vm.Cpu, Is.EqualTo(original));
    }

    [Test]
    public void TestNopAdvancesIpByOne() {
        Execute(0x4d);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(1u));
    }

    [Test]
    public void TestNopDoesNotTouchMemory() {
        for (uint i = 0; i < 64; i++) _vm.Memory[i] = (byte)(i ^ 0x55);
        byte[] before = (byte[])_vm.Memory.Clone();
        Execute(0x4d);
        // Execute() loads `[0x4d]` into address 0; restore that to compare.
        before[0] = 0x4d;
        Assert.That(_vm.Memory, Is.EqualTo(before));
    }
}
