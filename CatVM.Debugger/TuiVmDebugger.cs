using System.Globalization;
using CatData;

namespace CatVM.Debugger;

public class TuiVmDebugger {
    public CatDebugger Debugger { get; }
    private CatVm Vm => Debugger.Vm;

    public TuiVmDebugger(CatDebugger debugger) {
        Debugger = debugger;
    }

    public TuiVmDebugger(CatVm vm, string romPath) {
        Debugger = new CatDebugger(vm, romPath);
    }

    public void StartUserDebugging() {
        List<(string name, uint addr, int size)> watches = [];

        while (true) {
            Console.WriteLine("===============================");
            Console.WriteLine(Vm.Cpu.Dump());

            // print the next 7 bytes in hex from ip
            Console.Write("Mem[IP]: ");
            for (int i = 0; i < 7; i++) {
                byte b = Vm.Read8(Vm.Cpu.Ip + (uint)i);
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
                        value = Vm.Read8(addr);
                        Console.WriteLine($"{value} 0x{value:X2} {(sbyte)value}");
                        break;
                    case 2:
                        value = Vm.Read16(addr);
                        Console.WriteLine($"{value} 0x{value:X4} {(short)value}");
                        break;
                    case 4:
                        value = Vm.ReadWord(addr);
                        Console.WriteLine($"{value} 0x{value:X8} {(int)value}");
                        break;
                }
            }

            // try and find a symbol here
            DebugSymbol? symbol = Debugger.IpSymbol;
            if (symbol == null) {
                try {
                    string name = CatVm.OperationNames[Vm.Memory[Vm.Cpu.Ip]];
                    symbol = new DebugSymbol(0, "", 0, name);
                }
                catch (Exception) {
                    // ignore, just means we don't have a symbol for this instruction
                }
            }
            if (symbol == null) {
                Console.WriteLine("Unknown Symbol.");
            }
            else {
                Console.WriteLine($"=> {symbol.RawLine}");
                (string symbolFile, int symbolLine) = symbol.EffectiveLocation;
                if (symbolFile.Length != 0) {
                    Console.Write($"   at {symbolFile}:{symbolLine}");
                    // When the assembly was generated from a higher-level language, say where in
                    // the generated assembly we are too, so the two views can be lined up.
                    Console.WriteLine(symbol.SourceFile != null ? $" (asm {symbol.File}:{symbol.Line})" : string.Empty);
                }
            }

            Console.WriteLine("Stack trace:");
            foreach (uint frame in Debugger.CallStack) {
                string? name = Debugger.GetConstantWithValue(frame);
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
                    Debugger.Step();
                    break;
                }

                case "sout":
                case "step-out": {
                    if (Debugger.CallStack.Count == 0) {
                        Console.WriteLine("Not in a function call.");
                        break;
                    }

                    PrintStopReason(Debugger.StepOut());
                    break;
                }

                case "sover":
                case "step-over": {
                    PrintStopReason(Debugger.StepOver());
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
                    
                    Vm.Cpu.Set((byte)register, value);
                    Console.WriteLine($"Set register {register} to 0x{value:X8}");
                    break;
                }

                case "remove-bugs": {
                    Console.WriteLine("Feature not implemented yet.");
                    break;
                }

                case "cb":
                case "clear-breaks": {
                    Debugger.Breakpoints.Clear();
                    Console.WriteLine("Cleared all breakpoints.");
                    break;
                }

                case "b":
                case "break":
                case "breakpoint": {
                    if (parts.Length != 1 && parts.Length < 3) {
                        Console.WriteLine("break <symbol/line/addr> <point>   (line accepts <file>:<line>)");
                        continue;
                    }

                    uint? address = parts.Length == 1 ? Vm.Cpu.Ip : GetAddr(parts[1], parts[2]);
                    if (!address.HasValue) {
                        continue;
                    }

                    if (!Debugger.Breakpoints.Add(address.Value)) {
                        Debugger.Breakpoints.Remove(address.Value);
                        Console.WriteLine($"Removed breakpoint at 0x{address:X8}");
                        break;
                    }

                    Console.WriteLine($"Placed breakpoint at 0x{address:X8}");
                    break;
                }

                case "c":
                case "continue": {
                    RunUntil(() => false);
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
                            RunUntil(() => {
                                instructionsExecuted++;
                                return instructionsExecuted >= count;
                            });
                            break;
                        }

                        case "secs":
                        case "seconds": {
                            DateTime endTime = DateTime.Now.AddSeconds(count);
                            RunUntil(() => DateTime.Now >= endTime);
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

                    if (address.Value + size > Vm.Memory.Length) {
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

                    if (parts.Length >= 3) {
                        uint? address = GetAddr(parts[1], parts[2]);
                        if (!address.HasValue) {
                            continue;
                        }
                        addr = address.Value;
                    }

                    if (addr >= Vm.Memory.Length) {
                        Console.WriteLine("Address out of bounds.");
                        continue;
                    }

                    uint size = (uint)(Vm.Memory.Length - addr);
                    if (parts.Length >= 4) {
                        if (!TryParseNumber(parts[3], out size)
                            || size <= 0 
                            || addr + size > Vm.Memory.Length) {
                            Console.WriteLine($"Invalid size (Must be between 0 and {Vm.Memory.Length - addr}).");
                            continue;
                        }
                    }

                    Console.WriteLine($"Dumping memory from 0x{addr:X8} size {size} bytes.");

                    if (size <= 1024*1024) {  // print it in console
                        for (int i = 0; i < size; i += 16) {
                            Console.Write($"0x{addr + (uint)i:X8}: ");
                            for (int j = 0; j < 16 && i + j < size; j++) {
                                byte b = Vm.Read8(addr + (uint)(i + j));
                                Console.Write($"{b:X2} ");
                            }
                            Console.WriteLine();
                        }
                    }
                    else {
                        Console.WriteLine("Memory dump too large to display in console.");
                    }

                    const string dumpFile = "memory_dump.bin";
                    using FileStream fs = new(dumpFile, FileMode.Create, FileAccess.Write);
                    for (int i = 0; i < size; i++) {
                        byte b = Vm.Read8(addr + (uint)i);
                        fs.WriteByte(b);
                    }
                    Console.WriteLine($"Wrote memory dump to {dumpFile}.");
                    break;
                }

                case "h":
                case "help": {
                    Console.WriteLine("Debugger commands:");
                    Console.WriteLine(" step (s)                             - Step one instruction");
                    Console.WriteLine(" continue (c)                         - Continue execution until breakpoint or pause");
                    Console.WriteLine(" breakpoint (b) <type> <arg>          - Set/remove breakpoint at symbol/line/address (line takes <line> or <file>:<line>)");
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
                    if (!Debugger.DebugTable.Labels.TryGetValue(argValue, out uint address)) {
                        Console.WriteLine("Invalid symbol.");
                        return null;
                    }
                    return address;
                }

                case "l":
                case "line": {
                    // A bare line number is ambiguous as soon as a project uses #include, so
                    // <file>:<line> is accepted to pin it down.
                    string fileFilter = string.Empty;
                    string lineText = argValue;
                    int separator = argValue.LastIndexOf(':');
                    if (separator > 0) {
                        fileFilter = argValue[..separator];
                        lineText = argValue[(separator + 1)..];
                    }

                    if (!TryParseNumber(lineText, out uint line)) {
                        Console.WriteLine("Invalid line number.");
                        return null;
                    }

                    List<DebugSymbol> matches = [];
                    foreach (DebugSymbol sym in Debugger.DebugTable.Symbols) {
                        (string symbolFile, int symbolLine) = sym.EffectiveLocation;
                        if (symbolLine != line) continue;
                        if (fileFilter.Length != 0 && !FileMatches(symbolFile, fileFilter)) continue;
                        matches.Add(sym);
                    }

                    if (matches.Count == 0) {
                        Console.WriteLine("Could not find line number in symbols table.");
                        return null;
                    }

                    string[] files = matches
                        .Select(m => m.EffectiveLocation.File)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();

                    if (files.Length > 1) {
                        Console.WriteLine($"Line {line} is ambiguous across {files.Length} files:");
                        foreach (string file in files) {
                            Console.WriteLine($" - {file}:{line}");
                        }
                        Console.WriteLine("Disambiguate with <file>:<line>.");
                        return null;
                    }

                    // Lowest address so the breakpoint lands on the first instruction of the line.
                    return (uint)matches.Min(m => m.FilePos);
                }

                case "a":
                case "address":
                case "addr": {
                    if (TryParseNumber(argValue, out uint address)) return address;
                    Console.WriteLine("Invalid address.");
                    return null;
                }

                default:
                    Console.WriteLine("Invalid option.");
                    return null;
            }
        }
    }

    private static bool TryParseNumber(string str, out uint value) {
        str = str.Trim();
        if (str.StartsWith("0x")) {
            return uint.TryParse(str[2..], NumberStyles.HexNumber, null, out value);
        }
        return uint.TryParse(str, out value);
    }

    /// <summary>
    /// Matches a debug symbol's file against a user-supplied filter, accepting either a full
    /// path or just the file name so `break line display.cat:42` works without typing the path.
    /// </summary>
    private static bool FileMatches(string symbolFile, string filter) {
        if (symbolFile.Equals(filter, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return Path.GetFileName(symbolFile).Equals(Path.GetFileName(filter), StringComparison.OrdinalIgnoreCase);
    }

    private void PrintStopReason(CatDebugger.StopReason reason) {
        switch (reason) {
            case CatDebugger.StopReason.Breakpoint:
                Console.WriteLine($"Breakpoint: 0x{Vm.Cpu.Ip:X8}");
                break;

            case CatDebugger.StopReason.PausedVm:
                Console.WriteLine("VM has been paused.");
                break;

            case CatDebugger.StopReason.Predicate:
                break;
        }
    }

    private void RunUntil(Func<bool> predicate) {
        PrintStopReason(Debugger.RunUntil(predicate));
    }
}
