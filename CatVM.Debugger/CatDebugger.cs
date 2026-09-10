using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using CatData;

namespace CatVM.Debugger;

/// <summary>
/// Headless debugger core: breakpoints, a shadow call stack, and run/step primitives over a
/// <see cref="CatVm"/>. Front-ends (the TUI, the DAP adapter) sit on top of this and never
/// touch the VM's execution loop directly.
/// <p/>
/// Execution happens on whichever thread calls <see cref="RunUntil"/>; this class never spawns
/// threads and never writes to the console.
/// </summary>
// TODO: virtual address support
public class CatDebugger : IDisposable {
    public CatVm Vm { get; }
    public DebugTable DebugTable { get; }
    public DebugTableIndex Index { get; }

    /// <summary>
    /// Addresses to stop at. Only ever read on the executing thread; a front-end that changes
    /// breakpoints while the VM runs must marshal the change onto that thread.
    /// </summary>
    public HashSet<uint> Breakpoints { get; set; } = [];

    /// <summary>Shadow call stack, innermost frame first. Maintained by sniffing CALL/RET.</summary>
    public Stack<CallFrame> CallStack { get; } = new();

    /// <summary>Number of RETs seen with no frame to pop, for diagnostics.</summary>
    public int CallStackUnderflows { get; private set; }

    /// <summary>The guest CPU exception recorded by the most recent run that returned <see cref="StopReason.Fault"/>.</summary>
    public SpecialInterrupts? LastFault { get; private set; }
    public Exception? LastFaultException { get; private set; }

    /// <summary>
    /// After a <see cref="StopReason.Fault"/>: true when the guest had an interrupt handler
    /// installed and execution has been redirected to it, false when the default handler halted
    /// the VM instead.
    /// </summary>
    public bool FaultHandledByGuest { get; private set; }

    /// <summary>True once the guest has asked for the machine to shut down.</summary>
    public bool IsShutdown => _shutdown;

    public DebugSymbol? IpSymbol => GetSymbolAt(Vm.Cpu.Ip);

    private bool _faulted;
    private volatile bool _shutdown;

    private readonly Action<Exception, SpecialInterrupts> _faultHandler;
    private readonly Action _shutdownHandler;

    /// <summary>
    /// Debug a VM using the symbols beside its ROM (<c>&lt;romPath&gt;.debug</c>), or with no
    /// symbols when there is no such file. Check <see cref="HasDebugSymbols"/> to tell which.
    /// </summary>
    public CatDebugger(CatVm vm, string romPath)
        : this(vm, TryLoadDebugTable(romPath, out DebugTable table) ? table : new DebugTable([], [])) {
    }

    public CatDebugger(CatVm vm, DebugTable debugTable) {
        Vm = vm;
        DebugTable = debugTable;
        Index = new DebugTableIndex(debugTable);

        _faultHandler = (e, kind) => {
            _faulted = true;
            LastFault = kind;
            LastFaultException = e;
        };
        _shutdownHandler = () => _shutdown = true;
        Vm.OnCpuFault += _faultHandler;
        Vm.OnShutdown += _shutdownHandler;
    }

    public bool HasDebugSymbols => DebugTable.Symbols.Length != 0 || DebugTable.Labels.Count != 0;

    /// <summary>Loads <c>&lt;romPath&gt;.debug</c> if it exists.</summary>
    public static bool TryLoadDebugTable(string romPath, out DebugTable table) {
        return TryLoadDebugTableFile(romPath + ".debug", out table);
    }

    /// <summary>Loads a debug table from an explicit <c>.debug</c> file path.</summary>
    public static bool TryLoadDebugTableFile(string debugFilePath, out DebugTable table) {
        if (!File.Exists(debugFilePath)) {
            table = new DebugTable([], []);
            return false;
        }

        table = DebugTable.FromJsonNode(JsonNode.Parse(File.ReadAllText(debugFilePath))!);
        return true;
    }

    public StopReason RunToBreakpoint() {
        return RunUntil(() => false);
    }

    /// <summary>Execute exactly one instruction.</summary>
    public StopReason Step() {
        StopReason reason = StepOnce();
        if (reason == StopReason.Predicate && Breakpoints.Contains(Vm.Cpu.Ip)) {
            return StopReason.Breakpoint;
        }

        return reason;
    }

    public StopReason StepOut() {
        if (CallStack.Count == 0) {
            throw new InvalidOperationException("Cannot step out while not in function.");
        }

        int initialStackDepth = CallStack.Count;
        return RunUntil(() => CallStack.Count < initialStackDepth);
    }

    public StopReason StepOver() {
        byte opCode = Vm.Read8(Vm.Cpu.Ip);
        int initialStackDepth = CallStack.Count;

        // Step once so the call is processed, or if it isn't a call, just step normally.
        StopReason first = StepOnce();
        if (first != StopReason.Predicate) {
            return first;
        }

        // if it was a call, continue until we return to the same stack depth
        if (opCode == 0x3f) {
            // CALL
            return RunUntil(() => CallStack.Count <= initialStackDepth, true);
        }

        return Breakpoints.Contains(Vm.Cpu.Ip) ? StopReason.Breakpoint : StopReason.Predicate;
    }

    /// <summary>
    /// Run until <paramref name="predicate"/> returns true (checked before each instruction), a
    /// breakpoint is reached, the VM is paused from another thread, the guest faults, or the
    /// guest shuts the machine down. Always leaves <see cref="CatVm.Paused"/> true.
    /// </summary>
    /// <param name="handleFirstInstructionBreakpoints">
    /// Normally a breakpoint on the very first instruction is skipped so that continuing from a
    /// breakpoint doesn't immediately re-trigger it. Pass true to honour it.
    /// </param>
    public StopReason RunUntil(Func<bool> predicate, bool handleFirstInstructionBreakpoints = false) {
        if (_shutdown) {
            Vm.Paused = true;
            return StopReason.Shutdown;
        }

        StopReason? reason = null;
        _faulted = false;
        Vm.Paused = false;
        Vm.ExecuteWithErrorHandling(() => {
            bool firstInstruction = !handleFirstInstructionBreakpoints;
            while (true) {
                if (_shutdown) {
                    reason = StopReason.Shutdown;
                    break;
                }

                if (Vm.Paused) {
                    reason = StopReason.PausedVm;
                    break;
                }

                if (!firstInstruction && Breakpoints.Contains(Vm.Cpu.Ip)) {
                    reason = StopReason.Breakpoint;
                    break;
                }

                if (predicate()) {
                    reason = StopReason.Predicate;
                    break;
                }

                ProcessInstruction(Vm.Fast);
                firstInstruction = false;
            }
        });

        return FinishRun(reason);
    }

    /// <summary>Execute one instruction inside the VM's fault handling.</summary>
    private StopReason StepOnce() {
        if (_shutdown) {
            Vm.Paused = true;
            return StopReason.Shutdown;
        }

        _faulted = false;
        Vm.Paused = false;
        Vm.ExecuteWithErrorHandling(() => ProcessInstruction(Vm.Fast));
        return FinishRun(StopReason.Predicate);
    }

    private StopReason FinishRun(StopReason? reason) {
        if (_faulted) {
            // The fault has already been raised as a guest interrupt: either the guest's handler
            // is now the next thing to execute, or the default handler paused the VM.
            FaultHandledByGuest = !Vm.Paused;
            reason = StopReason.Fault;
        }
        else if (_shutdown) {
            reason = StopReason.Shutdown;
        }

        Vm.Paused = true;

        if (!reason.HasValue) {
            throw new Exception("RunUntil stopped for no reason");
        }

        return reason.Value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessInstruction(bool fast) {
        byte opCode = Vm.Read8(Vm.Cpu.Ip);
        switch (opCode) {
            case 0x3f: {
                // CALL
                byte addrReg = Vm.Read8(Vm.Cpu.Ip + 1);
                uint targetAddr = addrReg == 0xFF ? 0 : Vm.Cpu.Get(addrReg);
                uint offset = Vm.ReadWord(Vm.Cpu.Ip + 2);
                CallStack.Push(new CallFrame(targetAddr + offset, Vm.Cpu.Ip, Vm.Cpu.Ip + 6, Vm.Cpu.Sp - 4));
                break;
            }

            case 0x40: {
                // RET
                PopFrameForReturn();
                break;
            }
        }

        Vm.ExecuteInstruction(fast);
    }

    /// <summary>
    /// Pops the shadow frame a RET is about to return from. The return address on the guest
    /// stack is matched against recorded frames so the shadow stack resyncs after tail-call
    /// <c>jmp</c>s or code that edits its own return address; when nothing matches the top frame
    /// is popped as a best guess.
    /// </summary>
    private void PopFrameForReturn() {
        if (CallStack.Count == 0) {
            CallStackUnderflows++;
            return;
        }

        uint returnAddress;
        try {
            returnAddress = Vm.ReadWord(Vm.Cpu.Sp);
        }
        catch (Exception) {
            // The RET itself will fault; leave the shadow stack alone.
            return;
        }

        int depth = 0;
        bool found = false;
        foreach (CallFrame frame in CallStack) {
            depth++;
            if (frame.ReturnAddress == returnAddress) {
                found = true;
                break;
            }
        }

        if (!found) {
            depth = 1;
        }

        for (int i = 0; i < depth; i++) {
            CallStack.Pop();
        }
    }

    public DebugSymbol? GetSymbolAt(uint addr) {
        return Index.GetSymbolAt(addr);
    }

    public string? GetConstantWithValue(uint value) {
        return Index.LabelWithValue(value);
    }

    public void Dispose() {
        Vm.OnCpuFault -= _faultHandler;
        Vm.OnShutdown -= _shutdownHandler;
        GC.SuppressFinalize(this);
    }

    public enum StopReason {
        /// <summary>Stopped at an address in <see cref="Breakpoints"/>.</summary>
        Breakpoint,
        /// <summary>Something set <see cref="CatVm.Paused"/> while running: a pause request or the guest halting itself.</summary>
        PausedVm,
        /// <summary>The run predicate returned true (a step finished).</summary>
        Predicate,
        /// <summary>The guest raised a CPU exception; see <see cref="LastFault"/>.</summary>
        Fault,
        /// <summary>The guest shut the machine down.</summary>
        Shutdown
    }
}

/// <summary>One recorded CALL.</summary>
/// <param name="Callee">Address the call jumped to.</param>
/// <param name="CallSite">Address of the CALL instruction itself.</param>
/// <param name="ReturnAddress">Address the matching RET will return to.</param>
/// <param name="SpAfterCall">Stack pointer just after the return address was pushed.</param>
public readonly record struct CallFrame(uint Callee, uint CallSite, uint ReturnAddress, uint SpAfterCall);
