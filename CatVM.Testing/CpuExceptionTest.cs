namespace CatVM.Testing;

/// <summary>
/// Verifies that runtime exceptions inside <see cref="CatVm.ExecuteInstruction"/>
/// are translated into the correct CPU exception interrupts by
/// <see cref="CatVm.ExecuteWithErrorHandling"/>:
/// <list type="bullet">
///   <item><see cref="SpecialInterrupts.DivideByZero"/> (0x02)</item>
///   <item><see cref="SpecialInterrupts.InvalidInstruction"/> (0x01)</item>
///   <item><see cref="SpecialInterrupts.PageFault"/> (0x00) with the faulting
///         address pushed onto the stack</item>
/// </list>
/// Both the IT-routed path (custom handler) and the IT-less default path
/// (which halts the VM) are exercised.
/// </summary>
public class CpuExceptionTest {
    // Opcodes used in the tests.
    private const byte OpMovRI  = 0x01;  // mov r?, imm32
    private const byte OpMovIPI = 0x07;  // mov [imm32], imm32
    private const byte OpDivRR  = 0x1C;  // div r?, r?
    private const byte OpNop    = 0x4D;
    private const byte OpInvalid = 0xFF; // beyond Operations array length

    // Register IDs (from CatCpuState.Get/Set ordering).
    private const byte R4 = 0x04;
    private const byte R5 = 0x05;

    /// <summary>Build an IT with one (id → handlerAddr) entry.</summary>
    private static byte[] MakeIt(byte id, uint handlerAddr) {
        return [
            1,
            id,
            (byte)(handlerAddr & 0xFF),
            (byte)((handlerAddr >> 8) & 0xFF),
            (byte)((handlerAddr >> 16) & 0xFF),
            (byte)((handlerAddr >> 24) & 0xFF),
        ];
    }

    private static CatVm NewVm(int memory = 1024) {
        // Fast=true to skip sleep timing; DumpErrors=false to keep test output clean.
        return new CatVm(memory, 10_000) { Fast = true, DumpErrors = false };
    }

    /// <summary>
    /// Run a single instruction wrapped in <see cref="CatVm.ExecuteWithErrorHandling"/>
    /// so any exception is converted to its corresponding interrupt.
    /// </summary>
    private static void RunOne(CatVm vm) {
        vm.ExecuteWithErrorHandling(() => vm.ExecuteInstruction(fast: true));
    }

    // ---- DivideByZero ----------------------------------------------------

    [Test]
    public void DivideByZero_RoutesThroughItHandler() {
        CatVm vm = NewVm();
        const uint handlerAddr = 0x80;
        vm.LoadData([OpDivRR, R4, R5]);
        vm.LoadData([OpNop], handlerAddr);
        vm.LoadData(MakeIt((byte)SpecialInterrupts.DivideByZero, handlerAddr), 0x100);
        vm.Cpu.It = 0x100;
        vm.Cpu.R4 = 100; // dividend != 0
        vm.Cpu.R5 = 0;   // divisor == 0 ⇒ DivideByZeroException
        uint spBefore = vm.Cpu.Sp;

        RunOne(vm);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Ip, Is.EqualTo(handlerAddr),
                "IP should jump into the divide-by-zero handler");
            Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore - 5),
                "Return IP + marker should be pushed onto the stack");
            Assert.That(vm.Paused, Is.False,
                "Custom handler installed ⇒ default-handler halt should not fire");
        });
    }

    [Test]
    public void DivideByZero_WithoutItHandler_HaltsViaDefaultHandler() {
        CatVm vm = NewVm();
        vm.LoadData([OpDivRR, R4, R5]);
        vm.Cpu.R4 = 100;
        vm.Cpu.R5 = 0;
        // Cpu.It defaults to uint.MaxValue ⇒ no IT installed.

        RunOne(vm);

        Assert.That(vm.Paused, Is.True,
            "Default handler must halt the VM on a CPU exception interrupt");
    }

    [Test]
    public void DivideByZero_DividendZero_DoesNotThrow() {
        // The divide implementation short-circuits when the *dividend* is zero
        // (returning (0,0)) so the DivideByZeroException path is *not* taken
        // even when the divisor is also zero.
        CatVm vm = NewVm();
        vm.LoadData([OpDivRR, R4, R5]);
        vm.Cpu.R4 = 0;
        vm.Cpu.R5 = 0;

        RunOne(vm);

        Assert.Multiple(() => {
            Assert.That(vm.Paused, Is.False, "0/0 should not trigger the exception path");
            Assert.That(vm.Cpu.R4, Is.EqualTo(0u));
            Assert.That(vm.Cpu.R5, Is.EqualTo(0u));
        });
    }

    // ---- InvalidInstruction ---------------------------------------------

    [Test]
    public void InvalidInstruction_RoutesThroughItHandler() {
        CatVm vm = NewVm();
        const uint handlerAddr = 0x80;
        vm.LoadData([OpInvalid]);
        vm.LoadData([OpNop], handlerAddr);
        vm.LoadData(MakeIt((byte)SpecialInterrupts.InvalidInstruction, handlerAddr), 0x100);
        vm.Cpu.It = 0x100;
        uint spBefore = vm.Cpu.Sp;

        RunOne(vm);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Ip, Is.EqualTo(handlerAddr),
                "IP should jump into the invalid-instruction handler");
            Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore - 5),
                "Return IP + marker should be pushed onto the stack");
        });
    }

    [Test]
    public void InvalidInstruction_WithoutItHandler_HaltsViaDefaultHandler() {
        CatVm vm = NewVm();
        vm.LoadData([OpInvalid]);

        RunOne(vm);

        Assert.That(vm.Paused, Is.True,
            "Invalid opcode must trigger the default halting handler");
    }

    [Test]
    public void InvalidInstruction_PathDistinguishedFromGenericIndexOutOfRange() {
        // Invalid opcodes use a dedicated exception so dispatch-table failures cannot
        // be confused with memory IndexOutOfRangeExceptions.
        CatVm vm = NewVm();
        const uint invalidHandler = 0x80;
        const uint pageFaultHandler = 0xC0;
        // Install BOTH handlers; verify only the invalid-instruction one fires.
        vm.LoadData([OpInvalid]);
        vm.LoadData([OpNop], invalidHandler);
        vm.LoadData([OpNop], pageFaultHandler);
        // IT with two entries.
        byte[] it = [
            2,
            (byte)SpecialInterrupts.InvalidInstruction,
            (byte)(invalidHandler & 0xFF), (byte)((invalidHandler >> 8) & 0xFF),
            (byte)((invalidHandler >> 16) & 0xFF), (byte)((invalidHandler >> 24) & 0xFF),
            (byte)SpecialInterrupts.PageFault,
            (byte)(pageFaultHandler & 0xFF), (byte)((pageFaultHandler >> 8) & 0xFF),
            (byte)((pageFaultHandler >> 16) & 0xFF), (byte)((pageFaultHandler >> 24) & 0xFF),
        ];
        vm.LoadData(it, 0x100);
        vm.Cpu.It = 0x100;

        RunOne(vm);

        Assert.That(vm.Cpu.Ip, Is.EqualTo(invalidHandler),
            "Bad opcode must dispatch InvalidInstruction, not PageFault");
    }

    // ---- PageFault -------------------------------------------------------

    [Test]
    public void PageFault_OnOutOfRangeWrite_RoutesToHandler() {
        CatVm vm = NewVm(memory: 64);
        const uint handlerAddr = 32;
        // mov [0xFFFFFF00], 0x12345678 — the inner Memory[..] indexer throws
        // IndexOutOfRangeException, which the catch chain converts to PageFault.
        const uint badAddr = 0xFFFFFF00u;
        const uint value   = 0x12345678u;
        byte[] code = [
            OpMovIPI,
            (byte)(badAddr & 0xFF), (byte)((badAddr >> 8) & 0xFF),
            (byte)((badAddr >> 16) & 0xFF), (byte)((badAddr >> 24) & 0xFF),
            (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF),
            (byte)((value >> 16) & 0xFF), (byte)((value >> 24) & 0xFF),
        ];
        vm.LoadData(code);
        vm.LoadData([OpNop], handlerAddr);
        vm.LoadData(MakeIt((byte)SpecialInterrupts.PageFault, handlerAddr), 50);
        vm.Cpu.It = 50;
        uint spBefore = vm.Cpu.Sp;

        RunOne(vm);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Ip, Is.EqualTo(handlerAddr),
                "IP should jump into the page-fault handler");
            Assert.That(vm.Cpu.Sp, Is.LessThan(spBefore),
                "Stack pointer should have moved (return IP push)");
            Assert.That(vm.Paused, Is.False,
                "Custom handler installed ⇒ default-handler halt should not fire");
        });
    }

    [Test]
    public void PageFault_OnOutOfRangeRead_RoutesToHandler() {
        CatVm vm = NewVm(memory: 64);
        const uint handlerAddr = 32;
        const uint badAddr = 0xFFFFFF00;
        // mov r4, [badAddr]   (opcode 0x03 = MovRIP)
        byte[] code = [
            0x03, R4,
            (byte)(badAddr & 0xFF), (byte)((badAddr >> 8) & 0xFF),
            (byte)((badAddr >> 16) & 0xFF), (byte)((badAddr >> 24) & 0xFF),
        ];
        vm.LoadData(code);
        vm.LoadData([OpNop], handlerAddr);
        vm.LoadData(MakeIt((byte)SpecialInterrupts.PageFault, handlerAddr), 50);
        vm.Cpu.It = 50;

        RunOne(vm);

        Assert.That(vm.Cpu.Ip, Is.EqualTo(handlerAddr),
            "Out-of-range read should fault into the page-fault handler");
    }

    [Test]
    public void PageFault_WithoutItHandler_HaltsViaDefaultHandler() {
        CatVm vm = NewVm(memory: 64);
        // Out-of-range read via RIP from a stratospheric address.
        const uint badAddr = 0xFFFFFF00;
        byte[] code = [
            0x03, R4,
            (byte)(badAddr & 0xFF), (byte)((badAddr >> 8) & 0xFF),
            (byte)((badAddr >> 16) & 0xFF), (byte)((badAddr >> 24) & 0xFF),
        ];
        vm.LoadData(code);

        RunOne(vm);

        Assert.That(vm.Paused, Is.True,
            "Page fault with no handler must halt via the default handler");
    }

    // ---- Recovery via Ret ------------------------------------------------

    [Test]
    public void DivideByZeroHandler_CanReturnAndResumeFaultingInstruction() {
        // End-to-end: DIV by zero → handler runs → IRET pops back to the
        // faulting DIV. Verifies the saved IP is the *faulting* IP (since
        // Ip is only advanced at the end of a successful instruction, faults
        // observe the pre-instruction Ip).
        CatVm vm = NewVm();

        // Layout:
        //   0x00: div r4, r5      (3 bytes — re-executes after handler fixes divisor)
        //   0x03: nop             (executes once div completes successfully)
        //   0x80: iret            (handler — pops marker + IP and resumes)
        vm.LoadData([OpDivRR, R4, R5, OpNop]);
        vm.LoadData([0x52], 0x80); // 0x52 = Iret opcode
        vm.LoadData(MakeIt((byte)SpecialInterrupts.DivideByZero, 0x80), 0x100);
        vm.Cpu.It = 0x100;
        vm.Cpu.R4 = 1;
        vm.Cpu.R5 = 0;

        RunOne(vm);
        Assert.That(vm.Cpu.Ip, Is.EqualTo(0x80u), "handler entered");

        // Fix the divisor so the re-executed DIV will succeed.
        vm.Cpu.R5 = 1;

        // Step through: handler IRET should jump back to 0x00 (the DIV).
        vm.ExecuteInstruction(fast: true); // executes IRET
        Assert.That(vm.Cpu.Ip, Is.EqualTo(0x00u),
            "IRET in handler should return to the faulting DIV instruction");

        vm.ExecuteInstruction(fast: true); // re-executes DIV, now succeeds
        Assert.That(vm.Cpu.Ip, Is.EqualTo(0x03u),
            "DIV should advance IP by 3 once it completes without faulting");

        vm.ExecuteInstruction(fast: true); // executes the NOP
        Assert.That(vm.Cpu.Ip, Is.EqualTo(0x04u),
            "Following NOP should advance IP one byte");
    }
}
