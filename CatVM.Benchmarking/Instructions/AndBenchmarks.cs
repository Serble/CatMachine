using BenchmarkDotNet.Attributes;

namespace CatVM.Benchmarking.Instructions;

public class AndBenchmarks : InstructionBenchmarkBase {
    
    [IterationSetup(Target = "RunAndRR")]
    public void SetupAndRR() {
        Vm.Reset();
        Vm.LoadData([0x2b, 0x01, 0x02]);
    }
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunAndRR() => ExecuteTest();
    
    [IterationSetup(Target = "RunAndRI")]
    public void SetupAndRI() {
        Vm.Reset();
        Vm.LoadData([0x14, 0x01, 0x03, 0x00, 0x00, 0x00]);
    }
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunAndRI() => ExecuteTest();
}
