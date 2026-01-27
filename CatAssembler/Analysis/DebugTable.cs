namespace CatAssembler.Analysis;

public record DebugTable(DebugSymbol[] Symbols, Dictionary<string, uint> Labels);
