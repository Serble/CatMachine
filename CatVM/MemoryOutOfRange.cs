namespace CatVM;

public class MemoryOutOfRange(bool write, uint addr, uint length) : Exception {
    public uint Address { get; } = addr;
    public override string Message => $"Memory {(write ? "write" : "read")} out of range at address 0x{Address:X8} with length {length}.";
}
