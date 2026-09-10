namespace CatVM.DebugAdapter;

/// <summary>
/// stderr logging. stdout belongs to the protocol, so this is the only place diagnostics go.
/// Set <c>CATVM_DAP_STDERR_VERBOSE=1</c> to also log every message in both directions.
/// </summary>
public static class Log {
    public static bool Verbose { get; } = ReadVerboseFlag();

    private static readonly TextWriter Stderr_ = Console.Error;

    public static void Stderr(string message) {
        Stderr_.WriteLine($"[catvm-dap:{DateTime.UtcNow:O}] {message}");
        Stderr_.Flush();
    }

    private static bool ReadVerboseFlag() {
        string? value = Environment.GetEnvironmentVariable("CATVM_DAP_STDERR_VERBOSE");
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
    }
}
