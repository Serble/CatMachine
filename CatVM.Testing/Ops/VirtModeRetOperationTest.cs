namespace CatVM.Testing.Ops;

/// <summary>
/// Direct unit tests for the <c>iret</c> opcode dispatcher. The full
/// user/driver round-trip integration is covered in
/// <see cref="VirtualModeInterruptTest"/>; this file targets the opcode itself.
/// <list type="bullet">
///   <item>Marker 0x00 (kernel→kernel frame): pop IP only, leave everything else alone.</item>
///   <item>Unknown marker: raise InvalidInstruction.</item>
///   <item>iret in pure user mode (Mode=0b01) ProtectionFaults via the privilege gate.</item>
///   <item>iret in driver mode (Mode=0b11) is allowed (drivers may return from a
///         nested IT-resolved trap).</item>
/// </list>
/// </summary>
public class VirtModeRetOperationTest {
    private const byte OpIret = 0x52;
    private const byte InterruptFrameMarkerKernel     = 0x00;
    private const byte InterruptFrameMarkerSupervisor = 0x02;

    private static CatVM NewVm() => new(64 * 1024, 100_000) { Fast = true };

    [Test]
    public void Iret_KernelMarker_PopsIpOnly() {
        CatVM vm = NewVm();
        vm.LoadData([OpIret], 0x200);

        vm.Cpu.Mode = 0;
        vm.Cpu.Sp   = 0x1000;
        vm.Cpu.Ip   = 0x200;

        // Build the kernel→kernel frame using the public stack helpers.
        vm.StackPush(0xCAFEBABEu);
        vm.StackPush(InterruptFrameMarkerKernel);

        uint r0Before = vm.Cpu.R0 = 0xAAAA_BBBB;
        uint flBefore = vm.Cpu.Fl = 0x1234_5678;

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Ip, Is.EqualTo(0xCAFEBABEu),
                "Ip restored from kernel frame");
            Assert.That(vm.Cpu.Sp, Is.EqualTo(0x1000u),
                "Sp returned to pre-frame value (5 bytes popped)");
            Assert.That(vm.Cpu.R0, Is.EqualTo(r0Before),
                "kernel marker must NOT touch GP regs");
            Assert.That(vm.Cpu.Fl, Is.EqualTo(flBefore),
                "kernel marker must NOT touch flags");
            Assert.That(vm.Cpu.Mode, Is.EqualTo((byte)0),
                "Mode unchanged");
        });
    }

    [Test]
    public void Iret_UnknownMarker_RaisesInvalidInstruction() {
        CatVM vm = NewVm();
        vm.LoadData([OpIret], 0x200);

        vm.Cpu.Mode = 0;
        vm.Cpu.Sp   = 0x1000;
        vm.Cpu.Ip   = 0x200;
        vm.Cpu.It   = uint.MaxValue;

        vm.StackPush((byte)0x7F);  // bogus marker

        vm.ExecuteInstruction(fast: true);

        Assert.That(vm.Paused, Is.True,
            "unknown marker → InvalidInstruction → default handler halts");
    }

    [Test]
    public void Iret_InPureUserMode_FaultsViaPrivilegeGate() {
        CatVM vm = NewVm();
        const uint mbase = 0x1000;
        const uint mlen  = 0x100;
        vm.LoadData([OpIret], mbase);

        vm.Cpu.MBase = mbase;
        vm.Cpu.MLen  = mlen;
        vm.Cpu.Sp    = mlen;
        vm.Cpu.Ip    = 0;
        vm.Cpu.Mode  = 0b01;     // pure user
        vm.Cpu.It    = uint.MaxValue;

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Paused, Is.True,
                "user-mode iret should ProtectionFault → halt");
            Assert.That(vm.Cpu.Mode, Is.EqualTo((byte)0b01),
                "Mode must not have been changed by a faulted iret");
        });
    }

    [Test]
    public void Iret_InDriverMode_AllowedAndRestoresFromMarker() {
        // Driver (Mode=0b11) IRETing from a supervisor frame: should restore Mode=0b11
        // and the full saved process state.
        CatVM vm = NewVm();
        vm.LoadData([OpIret], 0x300);

        vm.Cpu.Mode = 0;          // we're inside the kernel handler
        vm.Cpu.Sp   = 0x1500;
        vm.Cpu.Ip   = 0x300;

        // Same push order as BuildInterruptFrameAndDispatch.
        vm.StackPush(0xAAAA_AAAAu);   // R0
        vm.StackPush(0xBBBB_BBBBu);   // R1
        vm.StackPush(0x2u);           // R2
        vm.StackPush(0x3u);           // R3
        vm.StackPush(0x4u);           // R4
        vm.StackPush(0x5u);           // R5
        vm.StackPush(0x6u);           // R6
        vm.StackPush(0x7777_7777u);   // R7
        vm.StackPush(0x0800u);        // MLen
        vm.StackPush(0x4000u);        // MBase
        vm.StackPush(0x1234_5678u);   // Fl
        vm.StackPush(0x07F0u);        // userSp
        vm.StackPush(0x0040u);        // userIp
        vm.StackPush(InterruptFrameMarkerSupervisor);

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Mode, Is.EqualTo((byte)0b11), "Mode restored to driver");
            Assert.That(vm.Cpu.Ip,    Is.EqualTo(0x40u));
            Assert.That(vm.Cpu.Sp,    Is.EqualTo(0x07F0u));
            Assert.That(vm.Cpu.Fl,    Is.EqualTo(0x1234_5678u));
            Assert.That(vm.Cpu.MBase, Is.EqualTo(0x4000u));
            Assert.That(vm.Cpu.MLen,  Is.EqualTo(0x0800u));
            Assert.That(vm.Cpu.R0,    Is.EqualTo(0xAAAA_AAAAu));
            Assert.That(vm.Cpu.R1,    Is.EqualTo(0xBBBB_BBBBu));
            Assert.That(vm.Cpu.R7,    Is.EqualTo(0x7777_7777u));
        });
    }
}
