namespace CatAssembler;

public record NeededLabel(uint Position, Func<uint, ReadOnlySpan<byte>> Transformer, int LineNum);