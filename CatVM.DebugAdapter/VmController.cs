using System.Collections.Concurrent;
using CatData;
using CatVM.Debugger;

namespace CatVM.DebugAdapter;

public enum VmCommand {
    /// <summary>First run after launch: like <see cref="Continue"/> but honours a breakpoint on the entry instruction.</summary>
    Launch,
    Continue,
    StepInstruction,
    NextInstruction,
    StepOut,
    StepLine,
    NextLine
}

public enum StopKind {
    Entry,
    Breakpoint,
    Step,
    Pause,
    /// <summary>The guest raised a CPU exception.</summary>
    Exception,
    /// <summary>The guest halted itself (HardwareManager halt, or an unhandled fault with no handler).</summary>
    Halted,
    Shutdown
}

public readonly record struct StopInfo(
    StopKind Kind,
    uint Ip,
    SpecialInterrupts? Fault,
    Exception? FaultException,
    bool FaultHandledByGuest);

/// <summary>
/// Owns the one thread the guest executes on. The protocol thread queues commands and reads VM
/// state only between them; every stop is reported through <c>onStopped</c> on the VM thread.
/// </summary>
public sealed class VmController : IDisposable {
    private readonly CatDebugger _debugger;
    private readonly Action<StopInfo> _onStopped;
    private readonly BlockingCollection<VmCommand> _commands = new();
    private readonly ConcurrentQueue<Action> _pending = new();
    private volatile bool _hasPending;
    private volatile bool _pauseRequested;
    private volatile bool _busy;
    private Thread? _thread;

    /// <summary>Read on the VM thread before deciding whether a guest fault stops the session.</summary>
    public Func<bool> BreakOnGuestFault { get; set; } = () => true;

    /// <summary>Line stepping compares assembly lines rather than the high-level source lines.</summary>
    public bool AssemblyView { get; set; }

    /// <summary>True from the moment a command is queued until its stop has been reported.</summary>
    public bool IsBusy => _busy;

    public bool Started => _thread != null;

    public VmController(CatDebugger debugger, Action<StopInfo> onStopped) {
        _debugger = debugger;
        _onStopped = onStopped;
    }

    public void Start() {
        if (_thread != null) return;
        _thread = new Thread(Loop) {
            Name = "catvm-dap-vm",
            IsBackground = true
        };
        _thread.Start();
    }

    public void Enqueue(VmCommand command) {
        _busy = true;
        _commands.Add(command);
    }

    /// <summary>
    /// Stop the current command as soon as possible. Safe to call at any time; a pause queued
    /// while nothing is running makes the next command stop immediately.
    /// </summary>
    public void RequestPause() {
        _pauseRequested = true;
        _debugger.Vm.Paused = true;
    }

    /// <summary>
    /// Run <paramref name="action"/> on the VM thread between instructions. Used for changes
    /// (breakpoint sets) that the executing loop reads without synchronisation.
    /// </summary>
    public void RunOnVmThread(Action action) {
        _pending.Enqueue(action);
        _hasPending = true;
        if (_thread == null || !_busy) {
            // Nothing is executing, so nothing races with us: apply now.
            DrainPending();
        }
    }

    /// <summary>Stop the VM thread. Returns once it has exited or the timeout passes.</summary>
    public void Terminate(TimeSpan timeout) {
        RequestPause();
        _commands.CompleteAdding();
        _thread?.Join(timeout);
    }

    private void Loop() {
        try {
            foreach (VmCommand command in _commands.GetConsumingEnumerable()) {
                StopInfo info;
                try {
                    info = Execute(command);
                }
                catch (Exception e) {
                    Log.Stderr($"VM thread: {command} failed: {e}");
                    info = new StopInfo(StopKind.Halted, _debugger.Vm.Cpu.Ip, null, e, false);
                }

                _busy = false;
                _onStopped(info);
            }
        }
        catch (Exception e) {
            Log.Stderr("VM thread died: " + e);
        }
    }

    private void DrainPending() {
        _hasPending = false;
        while (_pending.TryDequeue(out Action? action)) {
            try {
                action();
            }
            catch (Exception e) {
                Log.Stderr("Deferred VM action failed: " + e);
            }
        }
    }

    private Func<bool> Wrap(Func<bool> inner) {
        return () => {
            if (_hasPending) DrainPending();
            return _pauseRequested || inner();
        };
    }

    private StopInfo Execute(VmCommand command) {
        while (true) {
            if (_hasPending) DrainPending();

            if (_pauseRequested) {
                _pauseRequested = false;
                return Info(StopKind.Pause);
            }

            (CatDebugger.StopReason reason, StopKind stepKind) = command switch {
                VmCommand.Launch => (_debugger.RunUntil(Wrap(() => false), handleFirstInstructionBreakpoints: true), StopKind.Step),
                VmCommand.Continue => (_debugger.RunUntil(Wrap(() => false)), StopKind.Step),
                VmCommand.StepInstruction => (_debugger.Step(), StopKind.Step),
                VmCommand.NextInstruction => (NextInstruction(), StopKind.Step),
                VmCommand.StepOut => (StepOut(), StopKind.Step),
                VmCommand.StepLine => (StepLine(over: false), StopKind.Step),
                VmCommand.NextLine => (StepLine(over: true), StopKind.Step),
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            };

            if (reason == CatDebugger.StopReason.Fault && _debugger.FaultHandledByGuest && !BreakOnGuestFault()) {
                // The user doesn't want to see faults the guest handles itself: keep going into
                // the handler as if nothing happened.
                continue;
            }

            bool pauseRequested = _pauseRequested;
            _pauseRequested = false;

            StopKind kind = reason switch {
                CatDebugger.StopReason.Breakpoint => StopKind.Breakpoint,
                CatDebugger.StopReason.Predicate => pauseRequested ? StopKind.Pause : stepKind,
                CatDebugger.StopReason.PausedVm => pauseRequested ? StopKind.Pause : StopKind.Halted,
                CatDebugger.StopReason.Fault => StopKind.Exception,
                CatDebugger.StopReason.Shutdown => StopKind.Shutdown,
                _ => StopKind.Step
            };

            return Info(kind);
        }
    }

    private StopInfo Info(StopKind kind) {
        return new StopInfo(
            kind,
            _debugger.Vm.Cpu.Ip,
            kind == StopKind.Exception ? _debugger.LastFault : null,
            kind == StopKind.Exception ? _debugger.LastFaultException : null,
            kind == StopKind.Exception && _debugger.FaultHandledByGuest);
    }

    private CatDebugger.StopReason NextInstruction() {
        byte opCode = _debugger.Vm.Read8(_debugger.Vm.Cpu.Ip);
        int depth = _debugger.CallStack.Count;

        CatDebugger.StopReason first = _debugger.Step();
        if (first != CatDebugger.StopReason.Predicate || opCode != 0x3f) {
            return first;
        }

        // It was a CALL: run until the shadow stack is back to where it was.
        return _debugger.RunUntil(Wrap(() => _debugger.CallStack.Count <= depth), handleFirstInstructionBreakpoints: true);
    }

    private CatDebugger.StopReason StepOut() {
        int depth = _debugger.CallStack.Count;
        if (depth == 0) {
            throw new DapError("Not inside a function (no call has been recorded)");
        }

        return _debugger.RunUntil(Wrap(() => _debugger.CallStack.Count < depth));
    }

    /// <summary>
    /// Run until the current line changes. A line is the symbol's effective location (the
    /// high-level source line when there is one) unless <see cref="AssemblyView"/> is set.
    /// With <paramref name="over"/>, calls made from the line are run to completion first.
    /// </summary>
    private CatDebugger.StopReason StepLine(bool over) {
        if (!_debugger.HasDebugSymbols) {
            return over ? NextInstruction() : _debugger.Step();
        }

        (string, int)? start = LocationAt(_debugger.Vm.Cpu.Ip);
        int depth = _debugger.CallStack.Count;

        return _debugger.RunUntil(Wrap(() => {
            if (over && _debugger.CallStack.Count > depth) return false;
            (string, int)? here = LocationAt(_debugger.Vm.Cpu.Ip);
            return here != null && here != start;
        }));
    }

    private (string, int)? LocationAt(uint address) {
        if (!_debugger.Index.TryGetSymbolAt(address, out DebugSymbol symbol)) return null;
        return AssemblyView ? (symbol.File, symbol.Line) : symbol.EffectiveLocation;
    }

    public void Dispose() {
        Terminate(TimeSpan.FromSeconds(2));
        _commands.Dispose();
    }
}
