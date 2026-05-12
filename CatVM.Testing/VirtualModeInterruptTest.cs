namespace CatVM.Testing;

/// <summary>
/// Verifies the user→kernel interrupt frame and the <c>iret</c> opcode (Virtual Mode v1).
/// <para/>
/// Hardware interrupt path with <c>VirtualMode = 1</c>:
/// <list type="number">
///   <item>Sp swaps to Ksp.</item>
///   <item>VirtMode is cleared so the handler runs in kernel addressing.</item>
///   <item>R0..R7, MLen, MBase, Fl, userSp, Ip, marker are pushed (53 bytes).</item>
///   <item><c>iret</c> mirror-pops the frame and atomically restores Mode=1.</item>
/// </list>
/// </summary>
public class VirtualModeInterruptTest {
    private const byte OpNop  = 0x4D;
    private const byte OpIret = 0x52;

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
    public void UserMode_HardwareInterrupt_BuildsFullFrameAndIretRestores() {
        CatVm vm = NewVm();

        // Lay out a tiny "user program" at virtual 0 inside an MBase=0x4000 window of size 0x1000.
        // Code is just one NOP — we'll arrange for an IRQ to fire before it executes.
        const uint mbase = 0x4000;
        const uint mlen  = 0x1000;
        vm.LoadData([OpNop], mbase);          // user IP=0 → physical 0x4000
        vm.LoadData([OpNop, OpIret], 0x80);   // kernel handler at physical 0x80: NOP; IRET
        vm.LoadData(MakeIt(0x71, 0x80), 0x100);

        vm.Cpu.It    = 0x100;
        vm.Cpu.Ksp   = 0x2000;                // top of kernel stack (grows down)
        vm.Cpu.Ip    = 0;                     // user virtual 0
        vm.Cpu.Sp    = mlen;                  // user stack top (virtual)
        vm.Cpu.MBase = mbase;
        vm.Cpu.MLen  = mlen;
        vm.Cpu.R0    = 0xAAAAAAAA;
        vm.Cpu.R1    = 0xBBBBBBBB;
        vm.Cpu.R7    = 0x77777777;
        vm.Cpu.Fl    = 0x12345678;
        vm.Cpu.VirtualMode = true;            // "user mode"

        uint userSpBefore = vm.Cpu.Sp;
        uint userIpBefore = vm.Cpu.Ip;

        vm.HardwareInterrupt(SpecialInterrupts.HardwareTimerCallback); // 0x71
        vm.ExecuteInstruction(fast: true);    // dispatches IRQ + runs the handler's NOP

        // After dispatch + NOP we should be in kernel mode, on the kernel stack, at handler+1.
        Assert.Multiple(() => {
            Assert.That(vm.Cpu.VirtualMode, Is.False, "VirtMode should be cleared on entry");
            Assert.That(vm.Cpu.Ip, Is.EqualTo(0x81u), "should be one past handler entry");
            // Frame is 53 bytes (8*4 R-regs + 5*4 ctx + 4 ip + 1 marker = 32+20+1 = 53).
            // Sp moved from Ksp (0x2000) down by 53.
            Assert.That(vm.Cpu.Sp, Is.EqualTo(0x2000u - 53u),
                "Kernel Sp should be Ksp - 53 (full frame size)");
        });

        // Now run IRET.
        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.VirtualMode, Is.True, "iret should restore user mode");
            Assert.That(vm.Cpu.Ip, Is.EqualTo(userIpBefore), "user Ip restored");
            Assert.That(vm.Cpu.Sp, Is.EqualTo(userSpBefore), "user Sp restored");
            Assert.That(vm.Cpu.MBase, Is.EqualTo(mbase), "MBase restored");
            Assert.That(vm.Cpu.MLen, Is.EqualTo(mlen), "MLen restored");
            Assert.That(vm.Cpu.Fl, Is.EqualTo(0x12345678u), "Fl restored");
            Assert.That(vm.Cpu.R0, Is.EqualTo(0xAAAAAAAAu), "R0 restored");
            Assert.That(vm.Cpu.R1, Is.EqualTo(0xBBBBBBBBu), "R1 restored");
            Assert.That(vm.Cpu.R7, Is.EqualTo(0x77777777u), "R7 restored");
        });
    }

    [Test]
    public void KernelMode_HardwareInterrupt_StillUsesLegacyIpOnlyFrame() {
        // Sanity: with VirtMode off, the existing kernel→kernel path is unchanged
        // (Sp -= 4, no marker). Mirrors HardwareInterruptTest's basic dispatch shape.
        CatVm vm = NewVm();
        vm.LoadData([OpNop, OpNop]);
        vm.LoadData([OpNop], 0x40);
        vm.LoadData(MakeIt(0x71, 0x40), 0x100);

        vm.Cpu.It = 0x100;
        vm.Cpu.Ip = 0;
        vm.Cpu.VirtualMode = false;

        uint spBefore = vm.Cpu.Sp;
        vm.HardwareInterrupt(SpecialInterrupts.HardwareTimerCallback);
        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.VirtualMode, Is.False);
            Assert.That(vm.Cpu.Ip, Is.EqualTo(0x41u));
            Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore - 5u),
                "Kernel→kernel path pushes IP (4) + marker (1) = 5 bytes");
        });
    }

    [Test]
    public void KernelInterrupt_IretRoundTrips() {
        // After a kernel-mode interrupt, the handler should be able to `iret` cleanly
        // back to the pre-interrupt instruction, with Mode/Sp/regs untouched.
        CatVm vm = NewVm();
        vm.LoadData([OpNop, OpNop]);                       // post-interrupt code at IP=1
        vm.LoadData([OpIret], 0x40);                       // handler: just iret
        vm.LoadData(MakeIt(0x71, 0x40), 0x100);

        vm.Cpu.It = 0x100;
        vm.Cpu.Ip = 0;
        vm.Cpu.R3 = 0xC0DECAFE;
        vm.Cpu.VirtualMode = false;
        vm.InterruptsEnabled = true;

        uint spBefore = vm.Cpu.Sp;

        // Tick 1: dispatch IRQ → handler → iret all in one ExecuteInstruction.
        vm.HardwareInterrupt(SpecialInterrupts.HardwareTimerCallback);
        vm.ExecuteInstruction(fast: true);

        // Dispatch pushed marker+IP (5 bytes), then handler's iret popped them.
        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore),
                "iret should fully unwind the kernel frame");
            Assert.That(vm.Cpu.Ip, Is.EqualTo(0u),
                "iret should return to the IP that was preempted (0)");
            Assert.That(vm.Cpu.R3, Is.EqualTo(0xC0DECAFEu),
                "kernel-mode iret leaves GP regs alone");
            Assert.That(vm.Cpu.VirtualMode, Is.False,
                "kernel-mode iret leaves Mode alone");
        });
    }

    [Test]
    public void Iret_OnInvalidMarker_RaisesInvalidInstruction() {
        // Push a bogus marker (0xFF) onto the kernel stack and invoke iret.
        // Iret should refuse and raise InvalidInstruction (default handler halts the VM).
        CatVm vm = NewVm();
        vm.LoadData([OpIret]);
        vm.Cpu.Sp = 0x2000;
        vm.Cpu.VirtualMode = false;
        vm.Cpu.Ip = 0;

        vm.StackPush((byte)0xFF);             // simulate a corrupted top-of-stack marker

        // No IT installed → InvalidInstruction goes to DefaultHandler which halts the VM.
        vm.ExecuteInstruction(fast: true);

        Assert.That(vm.Paused, Is.True,
            "Iret with unknown marker should raise InvalidInstruction → default handler halts");
    }

    [Test]
    public void DriverMode_HardwareInterrupt_PushesSupervisorMarkerAndIretRestoresMode3() {
        // Driver mode = VirtualMode + SupervisorMode (Mode = 0b11). A driver runs in a
        // translated window like user code, but is privileged for IO opcodes etc. An IRQ
        // must push marker 0x02 and iret must restore Mode to exactly 0b11.
        CatVm vm = NewVm();

        const uint mbase = 0x4000;
        const uint mlen  = 0x1000;
        vm.LoadData([OpNop], mbase);
        vm.LoadData([OpNop, OpIret], 0x80);     // handler: NOP then IRET, so we can observe mid-handler state
        vm.LoadData(MakeIt(0x71, 0x80), 0x100);

        vm.Cpu.It    = 0x100;
        vm.Cpu.Ksp   = 0x2000;
        vm.Cpu.Ip    = 0;
        vm.Cpu.Sp    = mlen;
        vm.Cpu.MBase = mbase;
        vm.Cpu.MLen  = mlen;
        vm.Cpu.Mode  = 0b11;     // virtual + supervisor = driver

        uint userSpBefore = vm.Cpu.Sp;

        vm.HardwareInterrupt(SpecialInterrupts.HardwareTimerCallback);
        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.VirtualMode, Is.False, "VirtMode cleared on entry");
            Assert.That(vm.Cpu.SupervisorMode, Is.False,
                "Supervisor bit also cleared on entry — handler runs in canonical kernel mode");
            Assert.That(vm.Cpu.Ip, Is.EqualTo(0x81u), "in handler, post-NOP");
            Assert.That(vm.Cpu.Sp, Is.EqualTo(0x2000u - 53u), "full frame pushed");
            // Verify the marker is 0x02 by peeking the byte at Sp (top of stack).
            Assert.That(vm.Memory[vm.Cpu.Sp], Is.EqualTo(0x02),
                "topmost frame byte should be the supervisor marker (0x02)");
        });

        // iret
        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Mode, Is.EqualTo((byte)0b11),
                "iret(0x02) restores Mode to virtual+supervisor exactly");
            Assert.That(vm.Cpu.VirtualMode, Is.True);
            Assert.That(vm.Cpu.SupervisorMode, Is.True);
            Assert.That(vm.Cpu.Sp, Is.EqualTo(userSpBefore));
            Assert.That(vm.Cpu.MBase, Is.EqualTo(mbase));
            Assert.That(vm.Cpu.MLen, Is.EqualTo(mlen));
        });
    }

    [Test]
    public void DriverMode_PrivilegedOpcode_IsAllowed() {
        // A driver (Mode=0b11) executes a privileged opcode (setit) that would fault
        // if it were a plain user. Verifies TryPrivileged accepts SupervisorMode regardless
        // of VirtualMode.
        CatVm vm = NewVm();

        const uint mbase = 0x1000;
        const uint mlen  = 0x100;
        // User-virtual 0 → physical 0x1000. Layout: setit i 0xCAFE0000.
        const byte OpSetItI = 0x54;
        vm.LoadData([OpSetItI, 0x00, 0x00, 0xFE, 0xCA], mbase);

        vm.Cpu.MBase = mbase;
        vm.Cpu.MLen  = mlen;
        vm.Cpu.Sp    = mlen;
        vm.Cpu.Ip    = 0;
        vm.Cpu.Mode  = 0b11;     // driver

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Paused, Is.False, "no fault should fire");
            Assert.That(vm.Cpu.It, Is.EqualTo(0xCAFE0000u), "setit succeeded under driver mode");
            Assert.That(vm.Cpu.Mode, Is.EqualTo((byte)0b11),
                "driver mode preserved after non-trapping op");
        });
    }

    [Test]
    public void UserMode_PrivilegedOpcode_StillFaults() {
        // Sanity counterpart: pure user (Mode=0b01, no supervisor) hits ProtectionFault.
        CatVm vm = NewVm();

        const uint mbase = 0x1000;
        const uint mlen  = 0x100;
        const byte OpSetItI = 0x54;
        vm.LoadData([OpSetItI, 0x00, 0x00, 0xFE, 0xCA], mbase);

        vm.Cpu.MBase = mbase;
        vm.Cpu.MLen  = mlen;
        vm.Cpu.Sp    = mlen;
        vm.Cpu.Ip    = 0;
        vm.Cpu.Mode  = 0b01;     // pure user

        vm.ExecuteInstruction(fast: true);

        // No IT installed → ProtectionFault hits the default handler which halts.
        Assert.Multiple(() => {
            Assert.That(vm.Paused, Is.True,
                "user-mode privileged op should ProtectionFault → halt via default handler");
            Assert.That(vm.Cpu.It, Is.Not.EqualTo(0xCAFE0000u),
                "It must not have been written");
        });
    }
}
