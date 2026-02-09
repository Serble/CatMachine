namespace CatVM.Testing.Ops;

public class StackOperationTest : OperationTestBase {

    [Test]
    public void TestPushR() {
        _vm.Cpu.R1 = 0x12345678;
        Execute(0x20, 0x01);
        Assert.That(_vm.StackPop(), Is.EqualTo(0x12345678));
    }
    
    [Test]
    public void TestPushI() {
        Execute(0x21, 0x78, 0x56, 0x34, 0x12);
        Assert.That(_vm.StackPop(), Is.EqualTo(0x12345678));
    }
    
    [Test]
    public void TestPush16R() {
        _vm.Cpu.R1 = 0x12345678;
        Execute(0x22, 0x01);
        Assert.That(_vm.StackPop16(), Is.EqualTo(0x5678));
    }

    [Test]
    public void TestPush16I() {
        Execute(0x23, 0x78, 0x56);
        Assert.That(_vm.StackPop16(), Is.EqualTo(0x5678));
    }
    
    [Test]
    public void TestPush8R() {
        _vm.Cpu.R1 = 0x12345678;
        Execute(0x24, 0x01);
        Assert.That(_vm.StackPop8(), Is.EqualTo(0x78));
    }

    [Test]
    public void TestPush8I() {
        Execute(0x25, 0x78);
        Assert.That(_vm.StackPop8(), Is.EqualTo(0x78));
    }
    
    [Test]
    public void TestPopR() {
        _vm.StackPush(0x12345678);
        Execute(0x26, 0x01);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0x12345678));
    }

    [Test]
    public void TestPop16R() {
        _vm.StackPush((ushort)0x5678);
        Execute(0x27, 0x01);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0x5678));
    }

    [Test]
    public void TestPop8R() {
        _vm.StackPush((byte)0x78);
        Execute(0x28, 0x01);
        Assert.That(_vm.Cpu.R1, Is.EqualTo(0x78));
    }
    
    [Test]
    public void TestCall() {
        _vm.Cpu.R1 = 0;
        Execute(0x3f, 0x01, 0x00, 0x10, 0x00, 0x00);  // CALL R1, 0x1000
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x1000));
        Assert.That(_vm.StackPop(), Is.EqualTo(6)); // IP after reading instruction
    }
    
    [Test]
    public void TestRet() {
        _vm.StackPush(0x12345678);
        Execute(0x40);  // RET
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x12345678));
    }
}
