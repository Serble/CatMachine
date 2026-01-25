namespace CatVM;

public class MemoryOutOfRange(bool write, uint addr, uint length, string additionalMsg = "Out of range") : Exception {
    public uint Address { get; } = addr;
    public override string Message => $"Memory {(write ? "write" : "read")} " +
                                      $"invalid at address 0x{Address:X8} with length {length}: {additionalMsg}";
}
