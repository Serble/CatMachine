using BenchmarkDotNet.Attributes;

namespace CatVM.Benchmarking.Instructions;

public class DivBenchmarks : InstructionBenchmarkBase {
    
    [IterationSetup(Target = "RunDivRR")]
    public void SetupDivRR() {
        Vm.Reset();
        Vm.LoadData([0x1c, 0x01, 0x02]);
        Vm.Cpu.R1 = 100;
        Vm.Cpu.R2 = 3;
    }
    
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunDivRR() {
        for (int i = 0; i < Program.InstructionIterations; i++) {
            Vm.ExecuteInstruction(true);
            Vm.Cpu.Ip = 0;
            Vm.Cpu.R1 = 100;
            Vm.Cpu.R2 = 3;
        }
    }

    [IterationSetup(Target = "RunIDivRR")]
    public void SetupIDivRR() {
        Vm.Reset();
        Vm.LoadData([0x1d, 0x01, 0x02]);
        Vm.Cpu.R1 = 100;
        Vm.Cpu.R2 = 3;
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunIDivRR() {
        for (int i = 0; i < Program.InstructionIterations; i++) {
            Vm.ExecuteInstruction(true);
            Vm.Cpu.Ip = 0;
            Vm.Cpu.R1 = 100;
            Vm.Cpu.R2 = 3;
        }
    }
}
