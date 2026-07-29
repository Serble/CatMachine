namespace CatVM.Testing.Ops;

/// <summary>
/// Tests for <c>int</c>, <c>di</c>, <c>ei</c> opcodes and their privilege gates.
/// (<c>syscall</c> lives in <see cref="SyscallOperationTest"/>.)
/// </summary>
public class IntOperationTest : OperationTestBase {
    private const byte OpIntR = 0x1E;
    private const byte OpIntI = 0x1F;
    private const byte OpDi   = 0x45;
    private const byte OpEi   = 0x46;
    private const byte OpNop  = 0x4D;

    private static byte[] MakeIt(byte id, uint handlerAddr) {
        return [
            1, id,
            (byte)(handlerAddr & 0xFF),
            (byte)((handlerAddr >> 8) & 0xFF),
            (byte)((handlerAddr >> 16) & 0xFF),
            (byte)((handlerAddr >> 24) & 0xFF),
        ];
    }

    [Test]
    public void IntI_DispatchesIntoItHandler() {
        const uint handlerAddr = 0x80;
        _vm.LoadData([OpIntI, 0x42]);
        _vm.LoadData([OpNop], handlerAddr);
        _vm.LoadData(MakeIt(0x42, handlerAddr), 0x100);
        _vm.Cpu.It = 0x100;
        _vm.Cpu.Ip = 0;

        _vm.ExecuteInstruction();

        Assert.That(_vm.Cpu.Ip, Is.EqualTo(handlerAddr));
    }

    [Test]
    public void IntR_DispatchesIntoItHandler_LowByteOnly() {
        const uint handlerAddr = 0x80;
        _vm.LoadData([OpIntR, 0x01]);
        _vm.LoadData([OpNop], handlerAddr);
        _vm.LoadData(MakeIt(0x42, handlerAddr), 0x100);
        _vm.Cpu.It = 0x100;
        _vm.Cpu.Ip = 0;
        // Only the low byte of R1 should be used as the interrupt number.
        _vm.Cpu.R1 = 0xDEAD_BE42;

        _vm.ExecuteInstruction();

        Assert.That(_vm.Cpu.Ip, Is.EqualTo(handlerAddr));
    }

    [Test]
    public void Di_DisablesInterrupts() {
        _vm.InterruptsEnabled = true;
        Execute(OpDi);
        Assert.That(_vm.InterruptsEnabled, Is.False);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(1u));
    }

    [Test]
    public void Ei_EnablesInterrupts() {
        _vm.InterruptsEnabled = false;
        Execute(OpEi);
        Assert.That(_vm.InterruptsEnabled, Is.True);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(1u));
    }

    [Test]
    public void IntI_InUserMode_FaultsAndDoesNotInterrupt() {
        const uint mbase = 0x100;
        const uint mlen  = 0x100;
        _vm.LoadData([OpIntI, 0x42], mbase);
        _vm.Cpu.MBase = mbase;
        _vm.Cpu.MLen  = mlen;
        _vm.Cpu.Sp    = mlen;
        _vm.Cpu.Ip    = 0;
        _vm.Cpu.Mode  = 0b01;   // pure user
        _vm.Cpu.It    = uint.MaxValue;

        _vm.ExecuteInstruction();

        // ProtectionFault → default handler (no IT) halts.
        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.True);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(0u), "faulting int must not advance Ip");
        });
    }

    [Test]
    public void IntR_InUserMode_Faults() {
        const uint mbase = 0x100;
        const uint mlen  = 0x100;
        _vm.LoadData([OpIntR, 0x01], mbase);
        _vm.Cpu.R1 = 0x42;
        _vm.Cpu.MBase = mbase;
        _vm.Cpu.MLen  = mlen;
        _vm.Cpu.Sp    = mlen;
        _vm.Cpu.Ip    = 0;
        _vm.Cpu.Mode  = 0b01;
        _vm.Cpu.It    = uint.MaxValue;

        _vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.True);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(0u), "faulting int must not advance Ip");
        });
    }

    [Test]
    public void Di_InUserMode_FaultsAndLeavesInterruptsEnabled() {
        const uint mbase = 0x100;
        const uint mlen  = 0x100;
        _vm.LoadData([OpDi], mbase);
        _vm.Cpu.MBase = mbase;
        _vm.Cpu.MLen  = mlen;
        _vm.Cpu.Sp    = mlen;
        _vm.Cpu.Ip    = 0;
        _vm.Cpu.Mode  = 0b01;
        _vm.Cpu.It    = uint.MaxValue;
        _vm.InterruptsEnabled = true;

        _vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.True, "ProtectionFault halts via default handler");
            Assert.That(_vm.InterruptsEnabled, Is.True, "Di must not have taken effect");
        });
    }

    [Test]
    public void Ei_InUserMode_FaultsAndLeavesInterruptsDisabled() {
        const uint mbase = 0x100;
        const uint mlen  = 0x100;
        _vm.LoadData([OpEi], mbase);
        _vm.Cpu.MBase = mbase;
        _vm.Cpu.MLen  = mlen;
        _vm.Cpu.Sp    = mlen;
        _vm.Cpu.Ip    = 0;
        _vm.Cpu.Mode  = 0b01;
        _vm.Cpu.It    = uint.MaxValue;
        _vm.InterruptsEnabled = false;

        _vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.True);
            Assert.That(_vm.InterruptsEnabled, Is.False);
        });
    }

    [Test]
    public void Di_InDriverMode_IsAllowed() {
        const uint mbase = 0x100;
        const uint mlen  = 0x100;
        _vm.LoadData([OpDi], mbase);
        _vm.Cpu.MBase = mbase;
        _vm.Cpu.MLen  = mlen;
        _vm.Cpu.Sp    = mlen;
        _vm.Cpu.Ip    = 0;
        _vm.Cpu.Mode  = 0b11;   // driver: virtual + supervisor
        _vm.InterruptsEnabled = true;

        _vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.False);
            Assert.That(_vm.InterruptsEnabled, Is.False);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(1u));
        });
    }
}
