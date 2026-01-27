namespace CatVM.Debugging;

public record DebugTable(DebugSymbol[] Symbols, Dictionary<string, uint> Labels);
