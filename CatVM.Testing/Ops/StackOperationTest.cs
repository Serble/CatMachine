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

    [Test]
    public void TestPushPopRoundTrip_AllWidths() {
        _vm.Cpu.R1 = 0xDEADBEEF;
        _vm.LoadData([0x20, 0x01, 0x26, 0x02]);  // PUSH R1; POP R2
        _vm.Cpu.Ip = 0;
        _vm.ExecuteInstruction();
        _vm.ExecuteInstruction();
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0xDEADBEEFu));
    }

    [Test]
    public void TestPush16Pop16RoundTrip() {
        _vm.Cpu.R1 = 0xDEADBEEF;
        _vm.LoadData([0x22, 0x01, 0x27, 0x02]);  // PUSH16 R1; POP16 R2
        _vm.Cpu.Ip = 0;
        _vm.ExecuteInstruction();
        _vm.ExecuteInstruction();
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0xBEEFu));
    }

    [Test]
    public void TestPush8Pop8RoundTrip() {
        _vm.Cpu.R1 = 0xDEADBEEF;
        _vm.LoadData([0x24, 0x01, 0x28, 0x02]);  // PUSH8 R1; POP8 R2
        _vm.Cpu.Ip = 0;
        _vm.ExecuteInstruction();
        _vm.ExecuteInstruction();
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0xEFu));
    }

    [Test]
    public void TestCallWithNonZeroBase_AddsBaseAndOffset() {
        _vm.Cpu.R1 = 0x100;
        Execute(0x3f, 0x01, 0x20, 0x00, 0x00, 0x00);  // CALL R1, 0x20  → IP = 0x100 + 0x20
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x120u));
    }

    [Test]
    public void TestCallWithAbsoluteAddrReg0xFF() {
        Execute(0x3f, 0xFF, 0x42, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x42u));
    }

    [Test]
    public void TestPushDecrementsSpByCorrectWidth() {
        uint spBefore = _vm.Cpu.Sp;
        Execute(0x21, 0x78, 0x56, 0x34, 0x12);  // PUSH 0x12345678
        Assert.That(_vm.Cpu.Sp, Is.EqualTo(spBefore - 4));
    }

    [Test]
    public void TestPush8DecrementsSpByOne() {
        uint spBefore = _vm.Cpu.Sp;
        Execute(0x25, 0x78);  // PUSH8 0x78
        Assert.That(_vm.Cpu.Sp, Is.EqualTo(spBefore - 1));
    }

    [Test]
    public void TestPush16DecrementsSpByTwo() {
        uint spBefore = _vm.Cpu.Sp;
        Execute(0x23, 0x78, 0x56);
        Assert.That(_vm.Cpu.Sp, Is.EqualTo(spBefore - 2));
    }

    [Test]
    public void TestPushIncreasesThenPopRestoresSp() {
        uint spBefore = _vm.Cpu.Sp;
        _vm.LoadData([0x21, 0xEF, 0xBE, 0xAD, 0xDE, 0x26, 0x01]);
        _vm.Cpu.Ip = 0;
        _vm.ExecuteInstruction();
        _vm.ExecuteInstruction();
        Assert.That(_vm.Cpu.Sp, Is.EqualTo(spBefore));
    }

    [Test]
    public void TestPopWithoutPush_UnderflowsSp() {
        // Sp starts at memory size; popping without a push reads above it.
        uint spBefore = _vm.Cpu.Sp;
        _vm.LoadData([0x26, 0x01]);  // POP R1
        _vm.Cpu.Ip = 0;
        _vm.ExecuteWithErrorHandling(() => _vm.ExecuteInstruction(fast: true));
        // Reading at Sp == memory length is OOB → PageFault → halt.
        Assert.That(_vm.Cpu.Sp, Is.EqualTo(spBefore), "Sp must not advance on faulted pop");
    }

    [Test]
    public void TestPushFlPreservesValue() {
        _vm.Cpu.Fl = 0x0F;
        Execute(0x20, 0x0A);  // PUSH Fl (register index 10)
        Assert.That(_vm.StackPop(), Is.EqualTo(0x0Fu));
    }
}
