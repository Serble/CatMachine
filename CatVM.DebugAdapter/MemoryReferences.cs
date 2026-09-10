using System.Globalization;

namespace CatVM.DebugAdapter;

/// <summary>DAP <c>memoryReference</c> / <c>instructionReference</c> strings are plain hex addresses.</summary>
public static class MemoryReferences {
    public static string Encode(uint address) => "0x" + address.ToString("X8");

    public static bool TryDecode(string? reference, out uint address) {
        address = 0;
        if (string.IsNullOrWhiteSpace(reference)) return false;
        string text = reference.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            return uint.TryParse(text[2..], NumberStyles.HexNumber, null, out address);
        }

        return uint.TryParse(text, out address);
    }

    public static uint Decode(string? reference) {
        return TryDecode(reference, out uint address)
            ? address
            : throw new DapError($"Invalid memory reference '{reference}'");
    }
}
