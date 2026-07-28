namespace CatVM.AotTest;

/// <summary>
/// Helpers mirroring the NUnit <c>OperationTestBase</c> so opcode tests read the same way.
/// </summary>
public sealed class VmHarness(int memoryBytes = 512, uint cyclesPerSecond = 10_000, byte[]? rom = null) {
    public CatVm Vm { get; } = new(memoryBytes, cyclesPerSecond, rom) { Fast = true };

    /// <summary>Loads a single instruction at address 0 and executes it once.</summary>
    public void Execute(params byte[] data) {
        Vm.LoadData(data);
        Vm.Cpu.Ip = 0;
        Vm.ExecuteInstruction();
    }

    /// <summary>Loads the program at address 0 and steps it <paramref name="times"/> instructions.</summary>
    public void ExecuteN(int times, params byte[] data) {
        Vm.LoadData(data);
        Vm.Cpu.Ip = 0;
        for (int i = 0; i < times; i++) {
            Vm.ExecuteInstruction();
        }
    }
}
