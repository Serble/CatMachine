using CatData;

namespace CatVM.DebugAdapter;

/// <summary>A VM built for one debug session.</summary>
/// <param name="Vm">The machine, paused, with its ROM loaded and devices attached.</param>
/// <param name="DebugTable">Symbols for the ROM (empty when none were found).</param>
/// <param name="HasDebugSymbols">Whether <paramref name="DebugTable"/> came from a file.</param>
/// <param name="DebugTablePath">Where the symbols were looked for, for the user's benefit.</param>
/// <param name="Cleanup">Stops devices and releases resources once the session ends.</param>
public sealed record LaunchedVm(CatVm Vm, DebugTable DebugTable, bool HasDebugSymbols, string DebugTablePath, Func<Task> Cleanup);

/// <summary>
/// Builds the VM for a <c>launch</c> request. The launcher executable supplies one that reuses
/// its normal argument parsing and hardware discovery; tests supply an in-process one.
/// </summary>
public interface IVmLauncher {
    /// <exception cref="ArgumentException">The configuration was rejected; the message is shown to the user.</exception>
    LaunchedVm Launch(LaunchConfig config);
}
