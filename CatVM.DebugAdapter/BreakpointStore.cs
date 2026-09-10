using System.Text.Json.Nodes;
using CatData;
using CatVM.DebugAdapter.Protocol;
using CatVM.Debugger;

namespace CatVM.DebugAdapter;

/// <summary>
/// The client's breakpoints and where they landed. Breakpoints can be set before <c>launch</c>
/// (some clients do), so this exists from the start of the session and resolves everything once
/// <see cref="Attach"/> supplies the symbols.
/// </summary>
public sealed class BreakpointStore {
    private sealed class SourceBreakpoint(int id, int line, string? condition) {
        public int Id { get; } = id;
        public int Line { get; } = line;
        public string? Condition { get; } = condition;
        public uint? Address { get; set; }
        public DebugSymbol? Symbol { get; set; }
        public string? Message { get; set; }
    }

    private sealed class FunctionBreakpoint(int id, string name) {
        public int Id { get; } = id;
        public string Name { get; } = name;
        public uint? Address { get; set; }
        public string? Message { get; set; }
    }

    private sealed record InstructionBreakpoint(int Id, uint Address);

    private DebugTableIndex? _index;
    private SourceMapper? _mapper;
    private int _nextId = 1;
    private readonly Dictionary<string, List<SourceBreakpoint>> _source = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<FunctionBreakpoint> _function = [];
    private readonly List<InstructionBreakpoint> _instruction = [];

    public bool IsAttached => _index != null;

    /// <summary>
    /// Supply the symbols. Re-resolves breakpoints set before launch and returns the ones that
    /// changed, for <c>breakpoint</c> events.
    /// </summary>
    public JsonArray Attach(DebugTableIndex index, SourceMapper mapper) {
        _index = index;
        _mapper = mapper;

        List<JsonNode?> changed = [];
        foreach ((string path, List<SourceBreakpoint> list) in _source) {
            foreach (SourceBreakpoint bp in list) {
                ResolveSource(path, bp);
                changed.Add(SourceBreakpointJson(bp));
            }
        }

        foreach (FunctionBreakpoint bp in _function) {
            ResolveFunction(bp);
            changed.Add(FunctionBreakpointJson(bp));
        }

        return J.Arr(changed);
    }

    public JsonArray SetSourceBreakpoints(string path, IReadOnlyList<(int line, string? condition)> requested) {
        List<SourceBreakpoint> list = [];
        foreach ((int line, string? condition) in requested) {
            SourceBreakpoint bp = new(_nextId++, line, condition);
            ResolveSource(path, bp);
            list.Add(bp);
        }

        _source[path] = list;
        return J.Arr(list.Select(bp => (JsonNode?)SourceBreakpointJson(bp)));
    }

    public JsonArray SetFunctionBreakpoints(IReadOnlyList<string> names) {
        _function.Clear();
        foreach (string name in names) {
            FunctionBreakpoint bp = new(_nextId++, name);
            ResolveFunction(bp);
            _function.Add(bp);
        }

        return J.Arr(_function.Select(bp => (JsonNode?)FunctionBreakpointJson(bp)));
    }

    public JsonArray SetInstructionBreakpoints(IReadOnlyList<(string reference, int offset)> requested) {
        _instruction.Clear();
        List<JsonNode?> result = [];
        foreach ((string reference, int offset) in requested) {
            int id = _nextId++;
            if (!MemoryReferences.TryDecode(reference, out uint baseAddress)) {
                result.Add(J.Obj(("id", id), ("verified", false), ("message", $"Invalid instruction reference '{reference}'")));
                continue;
            }

            uint address = (uint)(baseAddress + offset);
            _instruction.Add(new InstructionBreakpoint(id, address));
            JsonObject json = J.Obj(("id", id), ("verified", true), ("instructionReference", MemoryReferences.Encode(address)));
            if (_index != null && _mapper != null && _index.TryGetSymbolAt(address, out DebugSymbol symbol)) {
                (_, int line) = _mapper.LocationOf(symbol);
                json["source"] = _mapper.SourceFor(symbol);
                json["line"] = line;
            }
            result.Add(json);
        }

        return J.Arr(result);
    }

    public HashSet<uint> BuildAddressSet() {
        HashSet<uint> set = [];
        foreach (List<SourceBreakpoint> list in _source.Values) {
            foreach (SourceBreakpoint bp in list) {
                if (bp.Address.HasValue) set.Add(bp.Address.Value);
            }
        }

        foreach (FunctionBreakpoint bp in _function) {
            if (bp.Address.HasValue) set.Add(bp.Address.Value);
        }

        foreach (InstructionBreakpoint bp in _instruction) {
            set.Add(bp.Address);
        }

        return set;
    }

    /// <summary>Ids of every breakpoint that resolves to <paramref name="address"/>.</summary>
    public int[] IdsAt(uint address) {
        List<int> ids = [];
        foreach (List<SourceBreakpoint> list in _source.Values) {
            ids.AddRange(list.Where(bp => bp.Address == address).Select(bp => bp.Id));
        }

        ids.AddRange(_function.Where(bp => bp.Address == address).Select(bp => bp.Id));
        ids.AddRange(_instruction.Where(bp => bp.Address == address).Select(bp => bp.Id));
        return ids.ToArray();
    }

    private void ResolveSource(string path, SourceBreakpoint bp) {
        bp.Address = null;
        bp.Symbol = null;
        bp.Message = null;

        if (_index == null || _mapper == null) {
            bp.Message = "Not launched yet";
            return;
        }

        if (bp.Condition != null) {
            bp.Message = "Conditional breakpoints are not supported; the condition is ignored";
        }

        LineResolution resolution = _mapper.ResolveClientLine(path, bp.Line);
        if (resolution.Matches.Length == 0) {
            bp.Message = _index.Table.Symbols.Length == 0
                ? "No debug symbols were loaded"
                : $"No code at line {bp.Line} of {Path.GetFileName(path)}";
            return;
        }

        if (resolution.IsAmbiguous) {
            bp.Message = $"Line {bp.Line} matches several files: {string.Join(", ", resolution.Files)}";
            return;
        }

        bp.Address = resolution.Address;
        bp.Symbol = resolution.Matches.MinBy(m => m.FilePos);
    }

    private void ResolveFunction(FunctionBreakpoint bp) {
        bp.Address = null;
        bp.Message = null;

        if (_index == null) {
            bp.Message = "Not launched yet";
            return;
        }

        if (!_index.TryGetLabel(bp.Name, out uint address)) {
            bp.Message = $"No label named '{bp.Name}'";
            return;
        }

        if (!_index.IsCodeLabel(bp.Name)) {
            bp.Message = $"'{bp.Name}' is a constant (0x{address:X8}), not a code label";
            return;
        }

        bp.Address = address;
    }

    private JsonObject SourceBreakpointJson(SourceBreakpoint bp) {
        JsonObject json = J.Obj(("id", bp.Id), ("verified", bp.Address.HasValue), ("line", bp.Line));
        if (bp.Message != null) json["message"] = bp.Message;
        if (bp.Address.HasValue) {
            json["instructionReference"] = MemoryReferences.Encode(bp.Address.Value);
            if (bp.Symbol != null && _mapper != null) json["source"] = _mapper.SourceFor(bp.Symbol);
        }

        return json;
    }

    private JsonObject FunctionBreakpointJson(FunctionBreakpoint bp) {
        JsonObject json = J.Obj(("id", bp.Id), ("verified", bp.Address.HasValue));
        if (bp.Message != null) json["message"] = bp.Message;
        if (bp.Address.HasValue) {
            json["instructionReference"] = MemoryReferences.Encode(bp.Address.Value);
            if (_index != null && _mapper != null && _index.TryGetSymbolAt(bp.Address.Value, out DebugSymbol symbol)) {
                (_, int line) = _mapper.LocationOf(symbol);
                json["source"] = _mapper.SourceFor(symbol);
                json["line"] = line;
            }
        }

        return json;
    }
}
