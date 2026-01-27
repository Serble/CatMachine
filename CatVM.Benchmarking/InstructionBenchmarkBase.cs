using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace CatVM.Benchmarking;

[SimpleJob(RunStrategy.Throughput)]
public abstract class InstructionBenchmarkBase {
    protected CatVM Vm = null!;
    
    [GlobalSetup]
    public void Setup() {
        Vm = new CatVM(Program.VmMemory, 1) {
            Fast = true
        };
    }

    protected void ExecuteTest() {
        for (int i = 0; i < Program.InstructionIterations; i++) {
            Vm.ExecuteInstruction(true);
            Vm.Cpu.Ip = 0;
        }
    }
}
