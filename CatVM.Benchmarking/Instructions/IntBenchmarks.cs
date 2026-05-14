using BenchmarkDotNet.Attributes;

namespace CatVM.Benchmarking.Instructions;

// Benchmarks for the interrupt-related instructions (IntR, IntI, Di, Ei, Syscall).
//
// Naming convention for benchmark methods:
//   Run<Op>_<StartingMode>_<WhatHappens>
// where <WhatHappens> spells out the entire path through HandleInterrupt:
//   * SystemHandler_<name>  — id matched a hard-coded 0x8X switch case in HandleInterrupt
//   * NoIT_DefaultIgnored   — Cpu.It == uint.MaxValue, DefaultHandler is invoked but
//                              the id is >= 0x10 so DefaultHandler fast-returns
//   * ITMiss_DefaultIgnored — IT installed but no entry matches; falls through to
//                              DefaultHandler which fast-returns (id >= 0x10)
//   * ITHit_KernelFrame     — IT entry matched, dispatcher took the kernel->kernel
//                              light-frame path (push Ip + marker only)
//   * ITHit_UserFrame       — IT entry matched, dispatcher took the user->kernel
//                              full-frame path (push all GP regs + MLen/MBase/Fl/Sp/Ip + marker)
//   * PrivFault_ITHit_UserFrame
//                           — instruction was issued in user mode, TryPrivileged
//                              raised ProtectionFault (0x03), and the IT has an entry
//                              for 0x03 so that fault is itself dispatched via the
//                              user->kernel full-frame path
//
// Why no PrivFault_NoIT / PrivFault_ITMiss variants: ProtectionFault is id 0x03,
// which is < 0x10, so DefaultHandler does NOT fast-return — it prints a register
// dump to the console and sets Paused. Both side effects would invalidate a
// throughput benchmark, so we always install an IT entry for 0x03 when measuring
// the privilege-fault path.
//
// Why no Print/Shutdown/Reset (0x80/0x82/0x83) system-handler benchmarks:
// they Console.Write, call Environment.Exit, or reset the whole VM — none safe
// for a 20M-iter loop. 0x81 (Halt) only flips Vm.Paused, which Fast-mode
// ExecuteInstruction doesn't check, so it's the only safe system-handler path.
public class IntBenchmarks : InstructionBenchmarkBase {

    // Address inside guest memory where the IT lives. Kernel-space (no virt
    // translation involved) and well clear of the instruction at address 0.
    private const uint ItAddress    = 0x100;
    private const uint HandlerAddr  = 0x200;

    // Lay out an interrupt table at ItAddress with a single entry mapping
    // `code` to `handler`. Format: count(byte), [code(byte) handler(uint LE)]*
    private void InstallInterruptTable(byte code, uint handler) {
        Vm.Memory[ItAddress + 0] = 0x01;          // entry count
        Vm.Memory[ItAddress + 1] = code;
        Vm.Memory[ItAddress + 2] = (byte)(handler & 0xFF);
        Vm.Memory[ItAddress + 3] = (byte)((handler >> 8) & 0xFF);
        Vm.Memory[ItAddress + 4] = (byte)((handler >> 16) & 0xFF);
        Vm.Memory[ItAddress + 5] = (byte)((handler >> 24) & 0xFF);
        Vm.Cpu.It = ItAddress;
    }

    // ---------------------------------------------------------------------
    // IntR (opcode 0x1E) — interrupt id sourced from a register. Privileged.
    // ---------------------------------------------------------------------

    // Kernel mode + no IT + id 0x10 -> DefaultHandler invoked, fast-returns
    // because id >= 0x10. Pure dispatch cost, no handler body, no IT scan.
    [IterationSetup(Target = nameof(RunIntR_Kernel_NoIT_DefaultIgnored))]
    public void SetupIntR_Kernel_NoIT_DefaultIgnored() {
        Vm.Reset();
        Vm.LoadData([0x1E, 0x01]);   // IntR R1
        Vm.Cpu.R1 = 0x10;
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunIntR_Kernel_NoIT_DefaultIgnored() => ExecuteTest();

    // Kernel mode + id 0x81 -> hits the hard-coded `case 0x81` in HandleInterrupt
    // before any IT logic runs. Handler body is just `vm.Paused = true`.
    [IterationSetup(Target = nameof(RunIntR_Kernel_SystemHandler_Halt))]
    public void SetupIntR_Kernel_SystemHandler_Halt() {
        Vm.Reset();
        Vm.LoadData([0x1E, 0x01]);   // IntR R1
        Vm.Cpu.R1 = 0x81;
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunIntR_Kernel_SystemHandler_Halt() => ExecuteTest();

    // Kernel mode + IT installed but with a different id -> walks the IT once,
    // misses, falls through to DefaultHandler which fast-returns (id 0x20 >= 0x10).
    // Measures IT-scan overhead with one entry.
    [IterationSetup(Target = nameof(RunIntR_Kernel_ITMiss_DefaultIgnored))]
    public void SetupIntR_Kernel_ITMiss_DefaultIgnored() {
        Vm.Reset();
        Vm.LoadData([0x1E, 0x01]);
        Vm.Cpu.R1 = 0x20;            // id we'll fire
        InstallInterruptTable(0x21, HandlerAddr);  // table only contains 0x21
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunIntR_Kernel_ITMiss_DefaultIgnored() => ExecuteTest();

    // Kernel mode + IT entry matches -> BuildInterruptFrameAndDispatch takes
    // the kernel->kernel branch (push Ip + 1-byte marker). Light frame.
    [IterationSetup(Target = nameof(RunIntR_Kernel_ITHit_KernelFrame))]
    public void SetupIntR_Kernel_ITHit_KernelFrame() {
        Vm.Reset();
        Vm.LoadData([0x1E, 0x01]);
        Vm.Cpu.R1 = 0x10;
        InstallInterruptTable(0x10, HandlerAddr);
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunIntR_Kernel_ITHit_KernelFrame() => ExecuteTestStackSafe();

    // User (virtual) mode -> TryPrivileged raises ProtectionFault (0x03).
    // We install an IT entry for 0x03 so the fault is dispatched through
    // BuildInterruptFrameAndDispatch's user->kernel full-frame path
    // (push R0..R7, MLen, MBase, Fl, Sp, Ip, marker; switch Sp to Ksp; clear Mode).
    // This measures the worst-case dispatch cost reachable from IntR.
    [IterationSetup(Target = nameof(RunIntR_User_PrivFault_ITHit_UserFrame))]
    public void SetupIntR_User_PrivFault_ITHit_UserFrame() {
        Vm.Reset();
        Vm.LoadData([0x1E, 0x01]);
        Vm.Cpu.R1 = 0x10;
        InstallInterruptTable(0x03, HandlerAddr);  // ProtectionFault = 0x03
        Vm.Cpu.Ksp = Vm.Cpu.Sp;
        Vm.Cpu.VirtualMode = true;
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunIntR_User_PrivFault_ITHit_UserFrame() => ExecuteTestModeSafe();

    // ---------------------------------------------------------------------
    // IntI (opcode 0x1F) — interrupt id encoded as immediate. Privileged.
    // Mirrors IntR's branch coverage exactly; only the operand source differs.
    // ---------------------------------------------------------------------

    [IterationSetup(Target = nameof(RunIntI_Kernel_NoIT_DefaultIgnored))]
    public void SetupIntI_Kernel_NoIT_DefaultIgnored() {
        Vm.Reset();
        Vm.LoadData([0x1F, 0x10]);
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunIntI_Kernel_NoIT_DefaultIgnored() => ExecuteTest();

    [IterationSetup(Target = nameof(RunIntI_Kernel_SystemHandler_Halt))]
    public void SetupIntI_Kernel_SystemHandler_Halt() {
        Vm.Reset();
        Vm.LoadData([0x1F, 0x81]);
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunIntI_Kernel_SystemHandler_Halt() => ExecuteTest();

    [IterationSetup(Target = nameof(RunIntI_Kernel_ITMiss_DefaultIgnored))]
    public void SetupIntI_Kernel_ITMiss_DefaultIgnored() {
        Vm.Reset();
        Vm.LoadData([0x1F, 0x20]);
        InstallInterruptTable(0x21, HandlerAddr);
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunIntI_Kernel_ITMiss_DefaultIgnored() => ExecuteTest();

    [IterationSetup(Target = nameof(RunIntI_Kernel_ITHit_KernelFrame))]
    public void SetupIntI_Kernel_ITHit_KernelFrame() {
        Vm.Reset();
        Vm.LoadData([0x1F, 0x10]);
        InstallInterruptTable(0x10, HandlerAddr);
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunIntI_Kernel_ITHit_KernelFrame() => ExecuteTestStackSafe();

    [IterationSetup(Target = nameof(RunIntI_User_PrivFault_ITHit_UserFrame))]
    public void SetupIntI_User_PrivFault_ITHit_UserFrame() {
        Vm.Reset();
        Vm.LoadData([0x1F, 0x10]);
        InstallInterruptTable(0x03, HandlerAddr);
        Vm.Cpu.Ksp = Vm.Cpu.Sp;
        Vm.Cpu.VirtualMode = true;
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunIntI_User_PrivFault_ITHit_UserFrame() => ExecuteTestModeSafe();

    // ---------------------------------------------------------------------
    // Di (opcode 0x45) — disable interrupts. Privileged.
    // Only two reachable paths: succeed in kernel, or take the priv-fault route.
    // ---------------------------------------------------------------------

    // Kernel mode -> TryPrivileged returns true, just clears InterruptsEnabled.
    // No interrupt dispatched. Measures the bare success path.
    [IterationSetup(Target = nameof(RunDi_Kernel_Success))]
    public void SetupDi_Kernel_Success() {
        Vm.Reset();
        Vm.LoadData([0x45]);
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunDi_Kernel_Success() => ExecuteTest();

    // User mode -> TryPrivileged raises ProtectionFault, dispatched via IT
    // through the user->kernel full-frame path (see IntR variant for details).
    [IterationSetup(Target = nameof(RunDi_User_PrivFault_ITHit_UserFrame))]
    public void SetupDi_User_PrivFault_ITHit_UserFrame() {
        Vm.Reset();
        Vm.LoadData([0x45]);
        InstallInterruptTable(0x03, HandlerAddr);
        Vm.Cpu.Ksp = Vm.Cpu.Sp;
        Vm.Cpu.VirtualMode = true;
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunDi_User_PrivFault_ITHit_UserFrame() => ExecuteTestModeSafe();

    // ---------------------------------------------------------------------
    // Ei (opcode 0x46) — enable interrupts. Privileged. Same shape as Di.
    // ---------------------------------------------------------------------

    [IterationSetup(Target = nameof(RunEi_Kernel_Success))]
    public void SetupEi_Kernel_Success() {
        Vm.Reset();
        Vm.LoadData([0x46]);
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunEi_Kernel_Success() => ExecuteTest();

    [IterationSetup(Target = nameof(RunEi_User_PrivFault_ITHit_UserFrame))]
    public void SetupEi_User_PrivFault_ITHit_UserFrame() {
        Vm.Reset();
        Vm.LoadData([0x46]);
        InstallInterruptTable(0x03, HandlerAddr);
        Vm.Cpu.Ksp = Vm.Cpu.Sp;
        Vm.Cpu.VirtualMode = true;
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunEi_User_PrivFault_ITHit_UserFrame() => ExecuteTestModeSafe();

    // ---------------------------------------------------------------------
    // Syscall (opcode 0x59) — non-privileged software interrupt to id 0x10.
    // No TryPrivileged check, so user mode reaches the dispatcher directly.
    // ---------------------------------------------------------------------

    // No IT -> DefaultHandler with id 0x10, fast-returns. Pure dispatch cost.
    [IterationSetup(Target = nameof(RunSyscall_Kernel_NoIT_DefaultIgnored))]
    public void SetupSyscall_Kernel_NoIT_DefaultIgnored() {
        Vm.Reset();
        Vm.LoadData([0x59]);
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunSyscall_Kernel_NoIT_DefaultIgnored() => ExecuteTest();

    // Kernel mode + IT entry for 0x10 -> kernel->kernel light-frame dispatch.
    [IterationSetup(Target = nameof(RunSyscall_Kernel_ITHit_KernelFrame))]
    public void SetupSyscall_Kernel_ITHit_KernelFrame() {
        Vm.Reset();
        Vm.LoadData([0x59]);
        InstallInterruptTable(0x10, HandlerAddr);
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunSyscall_Kernel_ITHit_KernelFrame() => ExecuteTestStackSafe();

    // User mode + IT entry for 0x10 -> user->kernel full-frame dispatch.
    // This is the canonical "userland program issues a syscall" case.
    [IterationSetup(Target = nameof(RunSyscall_User_ITHit_UserFrame))]
    public void SetupSyscall_User_ITHit_UserFrame() {
        Vm.Reset();
        Vm.LoadData([0x59]);
        InstallInterruptTable(0x10, HandlerAddr);
        Vm.Cpu.Ksp = Vm.Cpu.Sp;
        Vm.Cpu.VirtualMode = true;
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunSyscall_User_ITHit_UserFrame() => ExecuteTestModeSafe();
}
