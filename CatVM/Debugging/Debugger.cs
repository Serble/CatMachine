using System.Text.Json;
using CatData;

namespace CatVM.Debugging;

public class Debugger {
    private readonly CatVM _vm;
    private readonly DebugTable _table;

    public Debugger(CatVM vm, string romPath) {
        _vm = vm;

        _table = new DebugTable([], []);
        if (File.Exists(romPath + ".debug")) {
            _table = JsonSerializer.Deserialize<DebugTable>(File.ReadAllText(romPath + ".debug"))!;
            Console.WriteLine("Got debugging symbols.");
        }
        else {
            Console.WriteLine($"No debugging symbols found. ({romPath}.debug)");
        }
    }

    public DebugSymbol? GetSymbolAt(uint addr) {
        return _table.Symbols.FirstOrDefault(s => s.FilePos == addr);
    }
    
    public string? GetConstantWithValue(uint value) {
        foreach ((string key, uint val) in _table.Labels) {
            if (val == value) {
                return key;
            }
        }

        return null;
    }


    public void StartUserDebugging() {
        HashSet<uint> breakpoints = [];
        List<(string name, uint addr, int size)> watches = [];
        Stack<uint> callStack = [];
        
        while (true) {
            Console.WriteLine("===============================");
            Console.WriteLine(_vm.Cpu.Dump());
            
            // print the next 7 bytes in hex from ip
            Console.Write("Mem[IP]: ");
            for (int i = 0; i < 7; i++) {
                byte b = _vm.Read8(_vm.Cpu.Ip + (uint)i);
                Console.Write($"0x{b:X2}");

                if (i != 6) {
                    Console.Write(", ");
                }
            }
            Console.WriteLine();
            
            // watches
            foreach ((string name, uint addr, int size) in watches) {
                Console.Write($"Watch '{name}' at 0x{addr:X8}: ");
                uint value;
                switch (size) {
                    case 1:
                        value = _vm.Read8(addr);
                        Console.WriteLine($"{value} 0x{value:X2} {(sbyte)value}");
                        break;
                    case 2:
                        value = _vm.Read16(addr);
                        Console.WriteLine($"{value} 0x{value:X4} {(short)value}");
                        break;
                    case 4:
                        value = _vm.ReadWord(addr);
                        Console.WriteLine($"{value} 0x{value:X8} {(int)value}");
                        break;
                }
            }
            
            // try and find a symbol here
            DebugSymbol? symbol = GetSymbolAt(_vm.Cpu.Ip);
            Console.WriteLine(symbol != null ? $"=> {symbol.RawLine}" : "Unknown Symbol.");
            
            Console.WriteLine("Stack trace:");
            foreach (uint frame in callStack) {
                string? name = GetConstantWithValue(frame);
                Console.WriteLine(name != null ? $" - {name} (0x{frame:X8})" : $" - 0x{frame:X8}");
            }

            Console.WriteLine("===============================");
            Console.Write("Debugger> ");
            string? input = Console.ReadLine();
            if (input == null) continue;
            
            string[] parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;

            // TODO: modify memory, etc.
            switch (parts[0].ToLower()) {
                case "s":
                case "step": {
                    _vm.Paused = false;
                    ProcessInstruction(true);
                    _vm.Paused = true;
                    break;
                }

                case "sout":
                case "step-out": {
                    if (callStack.Count == 0) {
                        Console.WriteLine("Not in a function call.");
                        break;
                    }
                    
                    int initialStackDepth = callStack.Count;
                    ContinueUntil(() => callStack.Count < initialStackDepth);
                    break;
                }
                
                case "sover":
                case "step-over": {
                    byte opCode = _vm.Read8(_vm.Cpu.Ip);
                    int initialStackDepth = callStack.Count;
                    
                    // Step once so the call is processed
                    // or if it isn't a call, just step normally
                    _vm.Paused = false;
                    ProcessInstruction(true);
                    _vm.Paused = true;
                    
                    // if it was a call, continue until we return to the same stack depth
                    if (opCode == 0x3f) {
                        // CALL
                        Console.WriteLine("Stepping over function call...");
                        ContinueUntil(() => callStack.Count <= initialStackDepth);
                    }
                    break;
                }

                case "sr":
                case "set-register": {
                    if (parts.Length != 3) {
                        Console.WriteLine("set-register <reg> <value>");
                        continue;
                    }
                    
                    string regName = parts[1];
                    if (!Enum.TryParse(regName, true, out Register register)) {
                        Console.WriteLine("Invalid register name. Enter ID or name.");
                        continue;
                    }
                    
                    if (!uint.TryParse(parts[2], out uint value)) {
                        Console.WriteLine("Invalid value.");
                        continue;
                    }
                    
                    _vm.Cpu.Set((byte)register, value);
                    Console.WriteLine($"Set register {register} to 0x{value:X8}");
                    break;
                }

                case "remove-bugs": {
                    Console.WriteLine("Feature not implemented yet.");
                    break;
                }
                
                case "cb":
                case "clear-breaks": {
                    breakpoints.Clear();
                    Console.WriteLine("Cleared all breakpoints.");
                    break;
                }

                case "b":
                case "break":
                case "breakpoint": {
                    if (parts.Length != 1 && parts.Length < 3) {
                        Console.WriteLine("break <symbol/line/addr> <point>");
                        continue;
                    }
                    
                    uint? address = parts.Length == 1 ? _vm.Cpu.Ip : GetAddr(parts[1], parts[2]);
                    if (!address.HasValue) {
                        continue;
                    }

                    if (!breakpoints.Add(address.Value)) {
                        breakpoints.Remove(address.Value);
                        Console.WriteLine($"Removed breakpoint at 0x{address:X8}");
                        break;
                    }

                    Console.WriteLine($"Placed breakpoint at 0x{address:X8}");
                    break;
                }

                case "c":
                case "continue": {
                    ContinueUntil(() => false);
                    break;
                }

                case "cf":
                case "continue-for": {  // cf <instructions(instr)/seconds(secs)> <count>
                    if (parts.Length != 3) {
                        Console.WriteLine("continue-for <instructions(instr)/seconds(secs)> <count>");
                        continue;
                    }
                    
                    string mode = parts[1].ToLower();
                    if (!int.TryParse(parts[2], out int count) || count <= 0) {
                        Console.WriteLine("Invalid count.");
                        continue;
                    }
                    
                    switch (mode) {
                        case "instr":
                        case "instructions": {
                            int instructionsExecuted = 0;
                            ContinueUntil(() => {
                                instructionsExecuted++;
                                return instructionsExecuted >= count;
                            });
                            break;
                        }

                        case "secs":
                        case "seconds": {
                            DateTime endTime = DateTime.Now.AddSeconds(count);
                            ContinueUntil(() => DateTime.Now >= endTime);
                            break;
                        }

                        default:
                            Console.WriteLine("Invalid mode. Use 'instructions' or 'seconds'.");
                            break;
                    }
                    
                    break;
                }

                case "w":
                case "watch": {
                    if (parts.Length < 4) {
                        Console.WriteLine("watch <name> <symbol/line/addr> <point> [size]");
                        continue;
                    }
                    
                    string name = parts[1];
                    uint? address = GetAddr(parts[2], parts[3]);
                    
                    if (!address.HasValue) {
                        continue;
                    }
                    
                    int size = 4;
                    if (parts.Length >= 5) {
                        if (!int.TryParse(parts[4], out size) || (size != 1 && size != 2 && size != 4)) {
                            Console.WriteLine("Invalid size. Must be 1, 2, 4.");
                            continue;
                        }
                    }

                    if (address.Value + size > _vm.Memory.Length) {
                        Console.WriteLine("Watch exceeds memory bounds.");
                        continue;
                    }
                    
                    watches.Add((name, address.Value, size));
                    Console.WriteLine($"Added watch '{name}' at 0x{address:X8} of size {size} bytes.");
                    break;
                }

                case "dm":
                case "dumpmem":
                case "dump-memory": {  // dump-memory [symbol/line/addr] [point] [size]
                    uint addr = 0;
                    int size = _vm.Memory.Length;
                    
                    if (parts.Length >= 3) {
                        uint? address = GetAddr(parts[1], parts[2]);
                        if (!address.HasValue) {
                            continue;
                        }
                        addr = address.Value;
                    }
                    
                    if (parts.Length >= 4) {
                        if (!int.TryParse(parts[3], out size) || size <= 0) {
                            Console.WriteLine("Invalid size.");
                            continue;
                        }
                    }
                    
                    Console.WriteLine($"Dumping memory from 0x{addr:X8} size {size} bytes");

                    if (size <= 1024*1024) {  // print it in console
                        for (int i = 0; i < size; i += 16) {
                            Console.Write($"0x{addr + (uint)i:X8}: ");
                            for (int j = 0; j < 16 && i + j < size; j++) {
                                byte b = _vm.Read8(addr + (uint)(i + j));
                                Console.Write($"{b:X2} ");
                            }
                            Console.WriteLine();
                        }
                    }
                    
                    const string dumpFile = "memory_dump.bin";
                    using FileStream fs = new(dumpFile, FileMode.Create, FileAccess.Write);
                    for (int i = 0; i < size; i++) {
                        byte b = _vm.Read8(addr + (uint)i);
                        fs.WriteByte(b);
                    }
                    Console.WriteLine($"Wrote memory dump to {dumpFile}");
                    break;
                }
                
                case "h":
                case "help": {
                    Console.WriteLine("Debugger commands:");
                    Console.WriteLine(" step (s)                             - Step one instruction");
                    Console.WriteLine(" continue (c)                         - Continue execution until breakpoint or pause");
                    Console.WriteLine(" breakpoint (b) <type> <arg>          - Set/remove breakpoint at symbol/line/address");
                    Console.WriteLine(" clear-breaks (cb)                    - Clear all breakpoints");
                    Console.WriteLine(" watch (w) <name> <type> <arg> [size] - Add a watch at symbol/line/address with optional size (1,2,4)");
                    Console.WriteLine(" remove-bugs                          - Remove all bugs from the program (not implemented)");
                    Console.WriteLine(" step-over (sover)                    - Step over function calls");
                    Console.WriteLine(" step-out (sout)                      - Step out of the current function");
                    Console.WriteLine(" set-register (sr) <reg> <value>      - Set register to value");
                    Console.WriteLine(" dump-memory (dm) [type] [arg] [size] - Dump memory to file (and console if size <= 1024*1024) " +
                                      "from symbol/line/address with optional size");
                    Console.WriteLine(" continue-for (cf) <instructions(instr)/seconds(secs)> <count> - Continue for a number of instructions or seconds");
                    Console.WriteLine(" help (h)                             - Show this help message");
                    break;
                }
            }
        }

        uint? GetAddr(string argType, string argValue) {
            switch (argType.ToLower()) {
                case "s":
                case "symbol": {
                    if (!_table.Labels.TryGetValue(argValue, out uint address)) {
                        Console.WriteLine("Invalid symbol.");
                        return null;
                    }
                    return address;
                }

                case "l":
                case "line": {
                    if (!uint.TryParse(argValue, out uint line)) {
                        Console.WriteLine("Invalid line number.");
                        return null;
                    }

                    foreach (DebugSymbol sym in _table.Symbols) {
                        if (sym.Line != line) continue;
                                
                        return (uint)sym.FilePos;
                    }

                    Console.WriteLine("Could not find line number in symbols table.");
                    return null;
                }

                case "a":
                case "address":
                case "addr": {
                    if (!uint.TryParse(argValue, out uint address)) {
                        Console.WriteLine("Invalid line number.");
                        return null;
                    }
                    return address;
                }
                        
                default:
                    Console.WriteLine("Invalid option.");
                    return null;
            }
        }

        // predicate is slightly inefficient but this is a debugger after all
        void ContinueUntil(Func<bool> predicate) {
            _vm.Paused = false;
            _vm.ExecuteWithErrorHandling(() => {
                while (true) {
                    if (_vm.Paused) {
                        Console.WriteLine("VM has been paused.");
                        break;
                    }
                    
                    if (breakpoints.Contains(_vm.Cpu.Ip)) {
                        Console.WriteLine($"Breakpoint: 0x{_vm.Cpu.Ip:X8}");
                        break;
                    }
                    
                    if (predicate()) {
                        break;
                    }
                    
                    ProcessInstruction(_vm.Fast);
                }
            });
            _vm.Paused = true;
        }

        void ProcessInstruction(bool fast) {
            byte opCode = _vm.Read8(_vm.Cpu.Ip);
            switch (opCode) {
                case 0x3f: {
                    // CALL
                    byte addrReg = _vm.Read8(_vm.Cpu.Ip + 1);
                    uint targetAddr = addrReg == 0xFF ? 0 : _vm.Cpu.Get(addrReg);
                    uint offset = _vm.ReadWord(_vm.Cpu.Ip + 2);
                    callStack.Push(targetAddr + offset);
                    break;
                }

                case 0x40: {
                    // RET
                    if (callStack.Count > 0) {
                        callStack.Pop();
                    }
                    else {
                        Console.WriteLine("Call stack underflow on RET.");
                    }

                    break;
                }
            }
            
            _vm.ExecuteInstruction(fast);
        }
    }
}
