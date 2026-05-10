namespace CatVM.Testing.Ops;

public class DivOperationTest : OperationTestBase {
    
    [Test]
    public void TestUDivRR() {
        _vm.Cpu.R4 = 10;
        _vm.Cpu.R5 = 5;
        Execute(0x1c, 0x04, 0x05);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(2));
    }
    
    [Test]
    public void TestIDivRR() {
        _vm.Cpu.R4 = 10;
        _vm.Cpu.R5 = uint.MaxValue - 4;  // -5 in two's complement
        Execute(0x1d, 0x04, 0x05);
        Assert.That(_vm.Cpu.R4, Is.EqualTo(uint.MaxValue - 1));
    }

    [Test]
    public void TestUDivRemainder() {
        _vm.Cpu.R4 = 17;
        _vm.Cpu.R5 = 5;
        Execute(0x1c, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(3u));   // quotient
            Assert.That(_vm.Cpu.R5, Is.EqualTo(2u));   // remainder
        });
    }

    [Test]
    public void TestIDivRemainder_NegativeDividend() {
        _vm.Cpu.R4 = unchecked((uint)-17);
        _vm.Cpu.R5 = 5;
        Execute(0x1d, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(unchecked((uint)-3)));
            Assert.That(_vm.Cpu.R5, Is.EqualTo(unchecked((uint)-2)));
        });
    }

    [Test]
    public void TestUDivByZero_NonZeroDividend_RaisesDivideByZero() {
        _vm.LoadData([0x1c, 0x04, 0x05]);
        _vm.Cpu.Ip = 0;
        _vm.Cpu.R4 = 10;
        _vm.Cpu.R5 = 0;
        _vm.ExecuteWithErrorHandling(() => _vm.ExecuteInstruction(fast: true));
        // No IT installed → default handler halts on CPU exception interrupts.
        Assert.That(_vm.Paused, Is.True);
    }

    [Test]
    public void TestUDivByZero_ZeroDividend_NoException() {
        _vm.LoadData([0x1c, 0x04, 0x05]);
        _vm.Cpu.Ip = 0;
        _vm.Cpu.R4 = 0;
        _vm.Cpu.R5 = 0;
        _vm.ExecuteWithErrorHandling(() => _vm.ExecuteInstruction(fast: true));
        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.False);
            Assert.That(_vm.Cpu.R4, Is.EqualTo(0u));
            Assert.That(_vm.Cpu.R5, Is.EqualTo(0u));
        });
    }

    [Test]
    public void TestIDivByZero_RaisesDivideByZero() {
        _vm.LoadData([0x1d, 0x04, 0x05]);
        _vm.Cpu.Ip = 0;
        _vm.Cpu.R4 = unchecked((uint)-10);
        _vm.Cpu.R5 = 0;
        _vm.ExecuteWithErrorHandling(() => _vm.ExecuteInstruction(fast: true));
        Assert.That(_vm.Paused, Is.True);
    }

    [Test]
    public void TestIDivIntMinByMinusOne_Throws() {
        // INT_MIN / -1 overflows in .NET and throws OverflowException →
        // mapped via ExecuteWithErrorHandling.
        _vm.LoadData([0x1d, 0x04, 0x05]);
        _vm.Cpu.Ip = 0;
        _vm.Cpu.R4 = 0x80000000;
        _vm.Cpu.R5 = 0xFFFFFFFF;
        _vm.ExecuteWithErrorHandling(() => _vm.ExecuteInstruction(fast: true));
        // OverflowException is not caught specifically — falls through to generic
        // catch which raises InvalidInstruction → halt via default handler.
        Assert.That(_vm.Paused, Is.True);
    }

    [Test]
    public void TestUDivExactDivision_RemainderZero() {
        _vm.Cpu.R4 = 100;
        _vm.Cpu.R5 = 25;
        Execute(0x1c, 0x04, 0x05);
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R4, Is.EqualTo(4u));
            Assert.That(_vm.Cpu.R5, Is.EqualTo(0u));
        });
    }
}
