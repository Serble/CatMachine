using BenchmarkDotNet.Attributes;

namespace CatVM.Benchmarking.Instructions;

public class CpyBenchmarks : InstructionBenchmarkBase {

    private void Setup(byte[] data) {
        Vm.Reset();//cpy   r1,   r2
        Vm.LoadData(data);
        Vm.Cpu.R7 = 9;      // register for length value (should be constant across tests)
        Vm.Cpu.R0 = 0x100;  // destination addr needs to not be in our code area
    }

    [IterationSetup(Target = "RunCpyRR")]
    public void SetupCpyRR() => Setup([0x41, 0x01, 0x07]);
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunCmpRR() => ExecuteTest();
    
    [IterationSetup(Target = "RunCpyRI")]       //src   length
    public void SetupCpyRI() => Setup([0x42, 0x01, 0x09, 0x00, 0x00, 0x00]);
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunCpyRI() => ExecuteTest();
    
    [IterationSetup(Target = "RunCpyIR")]
    public void SetupCpyIR() => Setup([0x43, 0x10, 0x00, 0x00, 0x00, 0x07]);
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunCpyIR() => ExecuteTest();
    
    [IterationSetup(Target = "RunCpyII")]
    public void SetupCpyII() => Setup([0x44, 0x10, 0x00, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00]);
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunCpyII() => ExecuteTest();
}
