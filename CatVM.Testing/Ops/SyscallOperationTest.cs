namespace CatVM.Testing.Ops;

/// <summary>
/// Tests for the <c>syscall</c> opcode (0x59) — the only non-privileged interrupt
/// instruction. From user mode it must:
/// <list type="bullet">
///   <item>NOT raise ProtectionFault.</item>
///   <item>Raise <see cref="SpecialInterrupts.Syscall"/> (0x10).</item>
///   <item>Build a full user-frame and dispatch into the IT-resolved handler.</item>
/// </list>
/// Privileged <c>int</c> sanity-checks live alongside it.
/// </summary>
public class SyscallOperationTest {
    private const byte OpSyscall = 0x59;
    private const byte OpIntI    = 0x1F;     // 32nd opcode (index 31)

    private static byte[] MakeIt(byte id, uint handlerAddr) {
        return [
            1, id,
            (byte)(handlerAddr & 0xFF),
            (byte)((handlerAddr >> 8) & 0xFF),
            (byte)((handlerAddr >> 16) & 0xFF),
            (byte)((handlerAddr >> 24) & 0xFF),
        ];
    }

    private static CatVm NewVm() => new(64 * 1024, 100_000) { Fast = true };

    [Test]
    public void Syscall_FromUserMode_DispatchesIntoSyscallHandler() {
        CatVm vm = NewVm();

        const uint mbase = 0x4000;
        const uint mlen  = 0x1000;

        vm.LoadData([OpSyscall], mbase);                // user code at virtual 0
        vm.LoadData([0x4D /* nop */], 0x80);            // kernel handler at physical 0x80
        vm.LoadData(MakeIt((byte)SpecialInterrupts.Syscall, 0x80), 0x100);

        vm.Cpu.It    = 0x100;
        vm.Cpu.Ksp   = 0x2000;
        vm.Cpu.Ip    = 0;
        vm.Cpu.Sp    = mlen;
        vm.Cpu.MBase = mbase;
        vm.Cpu.MLen  = mlen;
        vm.Cpu.Mode  = 0b01;                            // pure user

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Paused, Is.False,
                "syscall must NOT ProtectionFault from user mode");
            // Software-interrupt path runs the syscall opcode then dispatches into the
            // handler; the handler's first instruction does NOT execute in the same tick
            // (unlike the hardware-IRQ path).
            Assert.That(vm.Cpu.Ip, Is.EqualTo(0x80u),
                "dispatched into Syscall handler at physical 0x80");
            Assert.That(vm.Cpu.Sp, Is.EqualTo(0x2000u - 53u),
                "full 53-byte user frame on the kernel stack");
        });
    }

    [Test]
    public void Syscall_FromDriverMode_DispatchesAndPushesSupervisorMarker() {
        CatVm vm = NewVm();

        const uint mbase = 0x4000;
        const uint mlen  = 0x1000;

        vm.LoadData([OpSyscall], mbase);
        vm.LoadData([0x4D], 0x80);
        vm.LoadData(MakeIt((byte)SpecialInterrupts.Syscall, 0x80), 0x100);

        vm.Cpu.It    = 0x100;
        vm.Cpu.Ksp   = 0x2000;
        vm.Cpu.Ip    = 0;
        vm.Cpu.Sp    = mlen;
        vm.Cpu.MBase = mbase;
        vm.Cpu.MLen  = mlen;
        vm.Cpu.Mode  = 0b11;                            // driver

        vm.ExecuteInstruction(fast: true);

        // Marker is the very last byte pushed (top of kernel stack).
        byte marker = vm.Read8Physical(vm.Cpu.Sp);

        Assert.Multiple(() => {
            Assert.That(vm.Paused,   Is.False);
            Assert.That(vm.Cpu.Mode, Is.EqualTo((byte)0));
            Assert.That(vm.Cpu.Ip,   Is.EqualTo(0x80u));
            Assert.That(marker,      Is.EqualTo((byte)0x02),
                "supervisor marker pushed on driver→kernel syscall");
        });
    }

    [Test]
    public void IntI_InUserMode_Faults() {
        // Counterpart sanity: the privileged `int N` opcode must still trap from user mode.
        CatVm vm = NewVm();
        const uint mbase = 0x1000;
        const uint mlen  = 0x100;
        vm.LoadData([OpIntI, 0x10], mbase);

        vm.Cpu.MBase = mbase;
        vm.Cpu.MLen  = mlen;
        vm.Cpu.Sp    = mlen;
        vm.Cpu.Ip    = 0;
        vm.Cpu.It    = uint.MaxValue;
        vm.Cpu.Mode  = 0b01;

        vm.ExecuteInstruction(fast: true);

        Assert.That(vm.Paused, Is.True,
            "user-mode `int` must ProtectionFault");
    }
}
