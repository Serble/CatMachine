namespace CatVM.Testing.Ops;

public class JmpOperationTest : OperationTestBase {

    [Test]
    public void TestJmp() {
        _vm.Cpu.R4 = 0;
        Execute(0x30, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x12));
    }

    [Test]
    public void TestJmpOffset() {
        _vm.Cpu.R4 = 5;
        Execute(0x30, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x17));
    }
    
    [Test]
    public void TestJz() {
        _vm.Cpu.ZeroFlag = true;
        _vm.Cpu.R4 = 5;
        Execute(0x35, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x17));  // hit
        
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x35, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
    }
    
    [Test]
    public void TestJnz() {
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x36, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x17));  // hit
        
        _vm.Cpu.ZeroFlag = true;
        _vm.Cpu.R4 = 5;
        Execute(0x36, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
    }

    [Test]
    public void TestJul() {  // unsigned less than
        _vm.Cpu.CarryFlag = true;
        _vm.Cpu.R4 = 5;
        Execute(0x37, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x17));  // hit
        
        _vm.Cpu.CarryFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x37, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
    }
    
    [Test]
    public void TestJule() {  // unsigned less than or equal
        _vm.Cpu.CarryFlag = true;
        _vm.Cpu.R4 = 5;
        Execute(0x38, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x17));  // hit
        
        _vm.Cpu.CarryFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x38, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
    }
    
    [Test]
    public void TestJug() {  // unsigned greater than
        _vm.Cpu.CarryFlag = false;
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x39, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x17));  // hit
        
        _vm.Cpu.CarryFlag = true;
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x39, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
        
        _vm.Cpu.CarryFlag = false;
        _vm.Cpu.ZeroFlag = true;
        _vm.Cpu.R4 = 5;
        Execute(0x39, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
    }
    
    [Test]
    public void TestJuge() {  // unsigned greater than or equal
        _vm.Cpu.CarryFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x3A, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x17));  // hit
        
        _vm.Cpu.CarryFlag = true;
        _vm.Cpu.R4 = 5;
        Execute(0x3A, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
    }
    
    [Test]
    public void TestJil() {  // signed less than
        _vm.Cpu.SignFlag = true;
        _vm.Cpu.OverflowFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x3b, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x17));  // hit
        
        _vm.Cpu.SignFlag = false;
        _vm.Cpu.OverflowFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x3b, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
        
        _vm.Cpu.SignFlag = true;
        _vm.Cpu.OverflowFlag = true;
        _vm.Cpu.R4 = 5;
        Execute(0x3b, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
    }
    
    [Test]
    public void TestJile() {  // signed less than or equal
        _vm.Cpu.ZeroFlag = true;
        _vm.Cpu.R4 = 5;
        Execute(0x3c, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x17));  // hit
        
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.SignFlag = true;
        _vm.Cpu.OverflowFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x3c, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x17));  // hit
        
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.SignFlag = false;
        _vm.Cpu.OverflowFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x3c, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
        
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.SignFlag = true;
        _vm.Cpu.OverflowFlag = true;
        _vm.Cpu.R4 = 5;
        Execute(0x3c, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
    }
    
    [Test]
    public void TestJig() {  // signed greater than
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.SignFlag = false;
        _vm.Cpu.OverflowFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x3d, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x17));  // hit
        
        _vm.Cpu.ZeroFlag = true;
        _vm.Cpu.SignFlag = false;
        _vm.Cpu.OverflowFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x3d, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
        
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.SignFlag = true;
        _vm.Cpu.OverflowFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x3d, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
        
        _vm.Cpu.ZeroFlag = false;
        _vm.Cpu.SignFlag = false;
        _vm.Cpu.OverflowFlag = true;
        _vm.Cpu.R4 = 5;
        Execute(0x3d, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
    }
    
    [Test]
    public void TestJige() {  // signed greater than or equal
        _vm.Cpu.SignFlag = false;
        _vm.Cpu.OverflowFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x3e, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x17));  // hit
        
        _vm.Cpu.SignFlag = true;
        _vm.Cpu.OverflowFlag = false;
        _vm.Cpu.R4 = 5;
        Execute(0x3e, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
        
        _vm.Cpu.SignFlag = false;
        _vm.Cpu.OverflowFlag = true;
        _vm.Cpu.R4 = 5;
        Execute(0x3e, 0x04, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x06));  // not hit (6 instruction bytes)
    }

    [Test]
    public void TestJmpAbsolute_AddrReg0xFF_TreatedAsZero() {
        // JmpOperation.Jmp special-cases addrReg == 0xFF as "no base" (absolute).
        _vm.Cpu.R4 = 0xDEAD;  // would otherwise pollute the target
        Execute(0x30, 0xFF, 0x12, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x12u));
    }

    [Test]
    public void TestJzAbsolute_AddrReg0xFF() {
        _vm.Cpu.ZeroFlag = true;
        Execute(0x35, 0xFF, 0x42, 0x00, 0x00, 0x00);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x42u));
    }

    [Test]
    public void TestCmpThenJugIntegration() {
        // CMP 10, 5 then JUG +0x10 should jump.
        _vm.Cpu.R4 = 10;
        _vm.Cpu.R5 = 5;
        _vm.Cpu.R6 = 0;
        _vm.LoadData([
            0x31, 0x04, 0x05,                 // CMP R4, R5
            0x39, 0x06, 0x10, 0x00, 0x00, 0x00 // JUG R6+0x10
        ]);
        _vm.Cpu.Ip = 0;
        _vm.ExecuteInstruction();
        _vm.ExecuteInstruction();
        // Jmp sets Ip = base(0) + offset(0x10) = 0x10
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x10u),
            "CMP set !CF && !ZF, JUG should jump (offset added to base 0)");
    }

    [Test]
    public void TestCmpThenJilIntegration_SignedLessThan() {
        // CMP -1, 1 (signed) → SF=1, OF=0, JIL should jump.
        _vm.Cpu.R4 = 0xFFFFFFFF; // -1
        _vm.Cpu.R5 = 1;
        _vm.Cpu.R6 = 0;
        _vm.LoadData([
            0x31, 0x04, 0x05,                 // CMP R4, R5
            0x3b, 0x06, 0x10, 0x00, 0x00, 0x00 // JIL R6+0x10
        ]);
        _vm.Cpu.Ip = 0;
        _vm.ExecuteInstruction();
        _vm.ExecuteInstruction();
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x10u));
    }
}
