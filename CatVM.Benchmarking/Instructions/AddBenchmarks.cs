using BenchmarkDotNet.Attributes;

namespace CatVM.Benchmarking.Instructions;

public class AddBenchmarks : InstructionBenchmarkBase {
    
    [IterationSetup(Target = "RunAddRR")]
    public void SetupAddRR() {
        Vm.Reset();
        Vm.LoadData([0x14, 0x01, 0x02]);
    }
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunAddRR() => ExecuteTest();
    
    [IterationSetup(Target = "RunAddRI")]
    public void SetupAddRI() {
        Vm.Reset();
        Vm.LoadData([0x15, 0x01, 0x03, 0x00, 0x00, 0x00]);
    }
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunAddRI() => ExecuteTest();
}
