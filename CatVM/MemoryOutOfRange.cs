namespace CatVM;

public class MemoryOutOfRange(uint addr) : Exception("Memory access out of range at address 0x" + addr.ToString("X8")) {
    public uint Address { get; } = addr;
}
