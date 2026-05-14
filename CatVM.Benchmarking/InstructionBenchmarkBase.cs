using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace CatVM.Benchmarking;

[SimpleJob(RunStrategy.Throughput)]
public abstract class InstructionBenchmarkBase {
    protected CatVm Vm = null!;
    
    [GlobalSetup]
    public void Setup() {
        Vm = new CatVm(Program.VmMemory, 1) {
            Fast = true
        };
    }

    protected void ExecuteTest() {
        for (int i = 0; i < Program.InstructionIterations; i++) {
            Vm.ExecuteInstruction(true);
            Vm.Cpu.Ip = 0;
        }
    }

    // Variant for instructions that may push to the stack (e.g. interrupt dispatch).
    // Snapshots Sp before the loop and restores it each iteration so the stack
    // doesn't deplete across the 20M iterations.
    protected void ExecuteTestStackSafe() {
        uint sp = Vm.Cpu.Sp;
        for (int i = 0; i < Program.InstructionIterations; i++) {
            Vm.ExecuteInstruction(true);
            Vm.Cpu.Ip = 0;
            Vm.Cpu.Sp = sp;
        }
    }

    // Variant for instructions that may also change CPU mode (e.g. user->kernel
    // interrupt dispatch). Restores Sp and Mode each iteration.
    protected void ExecuteTestModeSafe() {
        uint sp = Vm.Cpu.Sp;
        byte mode = Vm.Cpu.Mode;
        for (int i = 0; i < Program.InstructionIterations; i++) {
            Vm.ExecuteInstruction(true);
            Vm.Cpu.Ip = 0;
            Vm.Cpu.Sp = sp;
            Vm.Cpu.Mode = mode;
        }
    }
}
