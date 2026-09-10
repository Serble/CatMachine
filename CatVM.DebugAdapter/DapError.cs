namespace CatVM.DebugAdapter;

/// <summary>A request failed for a reason worth telling the user; becomes <c>success:false</c> with this message.</summary>
public sealed class DapError(string message) : Exception(message);
