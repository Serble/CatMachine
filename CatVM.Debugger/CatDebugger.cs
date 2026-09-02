using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using CatData;

namespace CatVM.Debugger;

// TODO: virtual address support
public class CatDebugger {
    public CatVm Vm { get; }
    public DebugTable DebugTable { get; }
    public HashSet<uint> Breakpoints { get; } = [];
    public Stack<uint> CallStack { get; } = new();

    public DebugSymbol? IpSymbol => GetSymbolAt(Vm.Cpu.Ip);

    public CatDebugger(CatVm vm, string romPath) {
        DebugTable table = new([], []);
        if (File.Exists(romPath + ".debug")) {
            table = DebugTable.FromJsonNode(JsonNode.Parse(File.ReadAllText(romPath + ".debug"))!);
            Console.WriteLine("Got debugging symbols.");
        }
        else {
            Console.WriteLine($"No debugging symbols found. ({romPath}.debug)");
        }

        Vm = vm;
        DebugTable = table;
    }

    public CatDebugger(CatVm vm, DebugTable debugTable) {
        Vm = vm;
        DebugTable = debugTable;
    }

    public void RunToBreakpoint() {
        RunUntil(() => false);
    }

    public void Step() {
        bool once = false;
        RunUntil(() => {
            if (once) {
                return true;
            }

            once = true;
            return false;
        });
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

        // Step once so the call is processed
        // or if it isn't a call, just step normally
        Vm.Paused = false;
        ProcessInstruction(true);
        Vm.Paused = true;

        // if it was a call, continue until we return to the same stack depth
        if (opCode == 0x3f) {
            // CALL
            return RunUntil(() => CallStack.Count <= initialStackDepth, true);
        }

        return StopReason.Predicate;
    }

    public StopReason RunUntil(Func<bool> predicate, bool handleFirstInstructionBreakpoints = false) {
        StopReason? reason = null;
        Vm.Paused = false;
        Vm.ExecuteWithErrorHandling(() => {
            bool firstInstruction = !handleFirstInstructionBreakpoints;
            while (true) {
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
                CallStack.Push(targetAddr + offset);
                break;
            }

            case 0x40: {
                // RET
                if (CallStack.Count > 0) {
                    CallStack.Pop();
                }
                else {
                    Console.WriteLine("Call stack underflow on RET.");
                }

                break;
            }
        }

        Vm.ExecuteInstruction(fast);
    }

    public DebugSymbol? GetSymbolAt(uint addr) {
        return DebugTable.Symbols.FirstOrDefault(s => s.FilePos == addr);
    }

    public string? GetConstantWithValue(uint value) {
        foreach ((string key, uint val) in DebugTable.Labels) {
            if (val == value) {
                return key;
            }
        }

        return null;
    }

    public enum StopReason {
        Breakpoint,
        PausedVm,
        Predicate
    }
}
