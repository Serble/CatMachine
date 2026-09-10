using System.Text;
using System.Text.Json.Nodes;
using CatData;
using CatVM.DebugAdapter.Protocol;
using CatVM.Debugger;

namespace CatVM.DebugAdapter;

/// <summary>
/// Disassembly driven by the debug table: each symbol's bytes run up to the next symbol's
/// address and its text is the source line that produced it. Bytes no symbol covers are shown
/// one per entry with the opcode name, since the VM has no instruction-length table.
/// </summary>
public sealed class Disassembler(CatVm vm, DebugTableIndex index, SourceMapper mapper) {
    /// <summary>
    /// Bytes shown for the very last symbol, whose length is unknown because nothing follows
    /// it in the table.
    /// </summary>
    private const int LastInstructionBytes = 7;

    private readonly record struct Unit(uint Address, int Length, DebugSymbol? Symbol);

    public JsonArray Disassemble(uint address, int instructionOffset, int count, bool resolveSymbols) {
        if (count <= 0) return new JsonArray();

        // Walk from the anchor by instructionOffset units, clamping at either end of memory so a
        // client scrolling past the edges still gets what exists.
        Unit? current = UnitAt(address);
        for (int i = 0; i < instructionOffset && current != null; i++) {
            Unit? next = Next(current.Value);
            if (next == null) break;
            current = next;
        }
        for (int i = 0; i > instructionOffset && current != null; i--) {
            Unit? previous = Previous(current.Value);
            if (previous == null) break;
            current = previous;
        }

        List<JsonNode?> entries = [];
        string? lastFile = null;
        Unit? unit = current;
        while (entries.Count < count) {
            if (unit == null) {
                // Before the start of memory: nothing to show, and the client copes with fewer.
                break;
            }

            entries.Add(Entry(unit.Value, resolveSymbols, ref lastFile));
            unit = Next(unit.Value);
        }

        return J.Arr(entries);
    }

    private Unit? UnitAt(uint address) {
        if (address >= vm.Memory.Length) return null;

        int idx = index.IndexAtOrBefore(address);
        if (idx >= 0) {
            DebugSymbol symbol = index.SymbolsByAddress[idx];
            int length = LengthOf(idx);
            if (address < (uint)symbol.FilePos + (uint)length) {
                return new Unit((uint)symbol.FilePos, length, symbol);
            }
        }

        return new Unit(address, 1, null);
    }

    private Unit? Next(Unit unit) {
        ulong next = (ulong)unit.Address + (ulong)unit.Length;
        return next >= (ulong)vm.Memory.Length ? null : UnitAt((uint)next);
    }

    private Unit? Previous(Unit unit) {
        if (unit.Address == 0) return null;
        return UnitAt(unit.Address - 1);
    }

    private int LengthOf(int symbolIndex) {
        int length = index.InstructionLength(symbolIndex);
        if (length > 0) return length;
        long remaining = vm.Memory.Length - index.SymbolsByAddress[symbolIndex].FilePos;
        return (int)Math.Max(1, Math.Min(LastInstructionBytes, remaining));
    }

    private JsonObject Entry(Unit unit, bool resolveSymbols, ref string? lastFile) {
        StringBuilder bytes = new();
        int shown = Math.Min(unit.Length, 16);
        for (int i = 0; i < shown; i++) {
            if (i != 0) bytes.Append(' ');
            bytes.Append(vm.Memory[unit.Address + i].ToString("X2"));
        }
        if (unit.Length > shown) bytes.Append(" ...");

        string instruction;
        if (unit.Symbol != null) {
            instruction = unit.Symbol.RawLine.Trim();
        }
        else {
            byte opcode = vm.Memory[unit.Address];
            instruction = opcode < CatVm.OperationNames.Length
                ? $"{CatVm.OperationNames[opcode].ToLowerInvariant()} ?"
                : $"db 0x{opcode:X2}";
        }

        JsonObject entry = J.Obj(
            ("address", MemoryReferences.Encode(unit.Address)),
            ("instructionBytes", bytes.ToString()),
            ("instruction", instruction));

        if (resolveSymbols) {
            string? label = index.CodeLabelAt(unit.Address);
            if (label != null) entry["symbol"] = label;
        }

        if (unit.Symbol != null && unit.Symbol.File.Length != 0) {
            // Disassembly always points at the assembly, never the high-level source: the
            // point of the view is to see the generated code. The file is only repeated when it
            // changes, as the protocol allows; the line is always given.
            if (!string.Equals(unit.Symbol.File, lastFile, StringComparison.OrdinalIgnoreCase)) {
                entry["location"] = mapper.SourceObject(unit.Symbol.File, unit.Symbol.SourceFile != null ? "generated assembly" : "catasm");
                lastFile = unit.Symbol.File;
            }
            entry["line"] = unit.Symbol.Line;
        }

        return entry;
    }
}
