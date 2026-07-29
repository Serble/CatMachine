namespace CatVM.Testing.Ops;

public class CmpOperationTest : OperationTestBase {

    [Test]
    public void Test() {
        RunCmp(5, 5, () => {
            Assert.That(_vm.Cpu.ZeroFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.False);
            Assert.That(_vm.Cpu.CarryFlag, Is.False);
            Assert.That(_vm.Cpu.OverflowFlag, Is.False);
        });
        
        RunCmp(10, 5, () => {
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.SignFlag, Is.False);
            Assert.That(_vm.Cpu.CarryFlag, Is.False);
            Assert.That(_vm.Cpu.OverflowFlag, Is.False);
        });
        
        RunCmp(5, 10, () => {
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.SignFlag, Is.True);
            Assert.That(_vm.Cpu.CarryFlag, Is.True);
            Assert.That(_vm.Cpu.OverflowFlag, Is.False);
        });

        // INT_MIN - 1 overflows: result = 0x7FFFFFFF (positive), no unsigned borrow.
        RunCmp(0x80000000, 1, () => {
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.SignFlag, Is.False);
            Assert.That(_vm.Cpu.CarryFlag, Is.False);
            Assert.That(_vm.Cpu.OverflowFlag, Is.True);
        });
    }

    private void RunCmp(uint v1, uint v2, Action expected) {
        RunCmpRR(v1, v2);
        expected();
        
        RunCmpRI(v1, v2);
        expected();
        
        RunCmpIR(v1, v2);
        expected();
        
        RunCmpII(v1, v2);
        expected();
    }

    // Force every flag into a known state that differs from any expected result so a
    // variant that fails to recompute the flags (e.g. a dropped Cmp call) is detected
    // instead of silently passing on the previous variant's leftover flags.
    private void PoisonFlags() {
        _vm.Cpu.ZeroFlag = true;
        _vm.Cpu.SignFlag = true;
        _vm.Cpu.CarryFlag = true;
        _vm.Cpu.OverflowFlag = true;
    }
    
    private void RunCmpRR(uint v1, uint v2) {
        _vm.Cpu.R4 = v1;
        _vm.Cpu.R5 = v2;
        PoisonFlags();
        Execute(0x31, 0x04, 0x05);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    private void RunCmpRI(uint v1, uint v2) {
        _vm.Cpu.R4 = v1;
        PoisonFlags();
        Execute(0x32, 0x04, (byte)(v2 & 0xFF), (byte)((v2 >> 8) & 0xFF), (byte)((v2 >> 16) & 0xFF), 
            (byte)((v2 >> 24) & 0xFF));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }
    
    private void RunCmpIR(uint v1, uint v2) {
        _vm.Cpu.R5 = v2;
        PoisonFlags();
        Execute(0x33, (byte)(v1 & 0xFF), (byte)((v1 >> 8) & 0xFF), (byte)((v1 >> 16) & 0xFF), 
            (byte)((v1 >> 24) & 0xFF), 0x05);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }
    
    private void RunCmpII(uint v1, uint v2) {
        PoisonFlags();
        Execute(0x34, (byte)(v1 & 0xFF), (byte)((v1 >> 8) & 0xFF), (byte)((v1 >> 16) & 0xFF), 
            (byte)((v1 >> 24) & 0xFF), (byte)(v2 & 0xFF), (byte)((v2 >> 8) & 0xFF), (byte)((v2 >> 16) & 0xFF), 
            (byte)((v2 >> 24) & 0xFF));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(9u));
    }

    [Test]
    public void TestCmpDoesNotMutateRegisters() {
        _vm.Cpu.R4 = 10;
        _vm.Cpu.R5 = 5;
        Execute(0x31, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(10u));
            Assert.That(_vm.Cpu.R5, Is.EqualTo(5u));
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }

    [Test]
    public void TestCmpSignedOverflow_IntMinVsOne() {
        // CMP INT_MIN, 1  →  signed overflow set; carry false; sign false (result = 0x7FFFFFFF)
        _vm.Cpu.R4 = 0x80000000;
        Execute(0x32, 0x04, 0x01, 0x00, 0x00, 0x00);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.OverflowFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.False);
            Assert.That(_vm.Cpu.CarryFlag, Is.False);
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
        });
    }

    [Test]
    public void TestCmpSignedOverflow_IntMaxVsNegOne() {
        // CMP 0x7FFFFFFF, -1  →  signed overflow set; carry set (unsigned)
        _vm.Cpu.R4 = 0x7FFFFFFF;
        _vm.Cpu.R5 = 0xFFFFFFFF;
        Execute(0x31, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.OverflowFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.True);
            Assert.That(_vm.Cpu.CarryFlag, Is.True);
            Assert.That(_vm.Cpu.ZeroFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }

    [Test]
    public void TestCmpZeroVsZero_AllFlagsCleanExceptZero() {
        Execute(0x34, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.ZeroFlag, Is.True);
            Assert.That(_vm.Cpu.SignFlag, Is.False);
            Assert.That(_vm.Cpu.CarryFlag, Is.False);
            Assert.That(_vm.Cpu.OverflowFlag, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(9u));
        });
    }
}
