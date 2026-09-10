using System.Text.Json.Nodes;
using CatData;
using CatVM.DebugAdapter.Protocol;
using CatVM.Debugger;

namespace CatVM.DebugAdapter;

/// <summary>
/// Turns the debugger's shadow call stack into DAP stack frames. Frame 0 is the current
/// instruction; frame k is the CALL that created frame k-1.
/// </summary>
public static class StackTraceBuilder {
    public static JsonArray Build(CatDebugger debugger, SourceMapper mapper, int startFrame, int levels, out int totalFrames) {
        CallFrame[] calls = debugger.CallStack.ToArray();   // innermost first
        totalFrames = calls.Length + 1;
        int end = levels <= 0 ? totalFrames : Math.Min(totalFrames, startFrame + levels);

        List<JsonNode?> frames = [];
        for (int k = Math.Max(0, startFrame); k < end; k++) {
            uint pc = k == 0 ? debugger.Vm.Cpu.Ip : calls[k - 1].CallSite;
            string name = k < calls.Length
                ? debugger.Index.CodeLabelAt(calls[k].Callee) ?? debugger.Index.FunctionContaining(pc) ?? $"0x{calls[k].Callee:X8}"
                : debugger.Index.FunctionContaining(pc) ?? "<entry>";

            JsonObject frame = J.Obj(
                ("id", k),
                ("name", name),
                ("column", 1),
                ("instructionPointerReference", MemoryReferences.Encode(pc)));

            DebugSymbol? symbol = debugger.Index.GetSymbolAt(pc);
            if (symbol == null) {
                int nearest = debugger.Index.IndexAtOrBefore(pc);
                if (nearest >= 0) symbol = debugger.Index.SymbolsByAddress[nearest];
            }

            if (symbol != null && mapper.LocationOf(symbol).File.Length != 0) {
                frame["source"] = mapper.SourceFor(symbol);
                frame["line"] = mapper.LocationOf(symbol).Line;
            }
            else {
                frame["line"] = 0;
                frame["presentationHint"] = "subtle";
            }

            frames.Add(frame);
        }

        return J.Arr(frames);
    }
}
