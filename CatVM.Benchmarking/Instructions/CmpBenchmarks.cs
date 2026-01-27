using BenchmarkDotNet.Attributes;

namespace CatVM.Benchmarking.Instructions;

public class CmpBenchmarks : InstructionBenchmarkBase {
    
    [IterationSetup(Target = "RunCmpRR")]
    public void SetupCmpRR() {
        Vm.Reset();
        Vm.LoadData([0x31, 0x01, 0x02]);
    }
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunCmpRR() => ExecuteTest();
    
    [IterationSetup(Target = "RunCmpRI")]
    public void SetupCmpRI() {
        Vm.Reset();
        Vm.LoadData([0x32, 0x01, 0x00, 0x00, 0x00, 0x05]);
    }
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunCmpRI() => ExecuteTest();
    
    [IterationSetup(Target = "RunCmpIR")]
    public void SetupCmpIR() {
        Vm.Reset();
        Vm.LoadData([0x33, 0x00, 0x00, 0x00, 0x0A, 0x02]);
    }
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunCmpIR() => ExecuteTest();
    
    [IterationSetup(Target = "RunCmpII")]
    public void SetupCmpII() {
        Vm.Reset();
        Vm.LoadData([0x34, 0x00, 0x00, 0x00, 0x0F, 0x00, 0x00, 0x00, 0x14]);
    }
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunCmpII() => ExecuteTest();
}
