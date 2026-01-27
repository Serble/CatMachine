using BenchmarkDotNet.Running;

namespace CatVM.Benchmarking;

public static class Program {
    public const int InstructionIterations = 20_000_000;
    public const int VmMemory = 1_050_576;  // display buffer plus 2kb

    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
