using BenchmarkDotNet.Attributes;

namespace CatVM.Benchmarking.Instructions;

/// <summary>
/// Benchmarks for every variant of the MOV family (word, short, byte). For pointer-based variants
/// we set <c>R3</c> to a scratch address (0x100) that doesn't overlap with the loaded program so
/// reads/writes are valid and don't corrupt the instruction being measured.
/// </summary>
public class MovBenchmarks : InstructionBenchmarkBase {

    private const uint ScratchAddr = 0x100;

    private void Setup(byte[] data) {
        Vm.Reset();
        Vm.LoadData(data);
        Vm.Cpu.R3 = ScratchAddr; // pointer register for *RP* / *PR* style ops
        Vm.Cpu.R1 = 0xCAFEBABE;  // value for source-register ops
    }

    // ---------------- Word (4 byte) MOV ----------------

    [IterationSetup(Target = "RunMovRR")]
    public void SetupMovRR() => Setup([0x00, 0x02, 0x01]); // MOV R2, R1

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunMovRR() => ExecuteTest();

    [IterationSetup(Target = "RunMovRI")]
    public void SetupMovRI() => Setup([0x01, 0x02, 0x05, 0x00, 0x00, 0x00]); // MOV R2, 5

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunMovRI() => ExecuteTest();

    [IterationSetup(Target = "RunMovRRP")]
    public void SetupMovRRP() => Setup([0x02, 0x02, 0x03]); // MOV R2, [R3]

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunMovRRP() => ExecuteTest();

    [IterationSetup(Target = "RunMovRIP")]
    public void SetupMovRIP() => Setup([0x03, 0x02, 0x00, 0x01, 0x00, 0x00]); // MOV R2, [0x100]

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunMovRIP() => ExecuteTest();

    [IterationSetup(Target = "RunMovRPR")]
    public void SetupMovRPR() => Setup([0x04, 0x03, 0x01]); // MOV [R3], R1

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunMovRPR() => ExecuteTest();

    [IterationSetup(Target = "RunMovRPI")]
    public void SetupMovRPI() => Setup([0x05, 0x03, 0x05, 0x00, 0x00, 0x00]); // MOV [R3], 5

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunMovRPI() => ExecuteTest();

    [IterationSetup(Target = "RunMovIPR")]
    public void SetupMovIPR() => Setup([0x06, 0x00, 0x01, 0x00, 0x00, 0x01]); // MOV [0x100], R1

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunMovIPR() => ExecuteTest();

    [IterationSetup(Target = "RunMovIPI")]
    public void SetupMovIPI() => Setup([0x07, 0x00, 0x01, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00]); // MOV [0x100], 5

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunMovIPI() => ExecuteTest();

    // ---------------- Short (2 byte) MOV ----------------

    [IterationSetup(Target = "RunSMovRRP")]
    public void SetupSMovRRP() => Setup([0x08, 0x02, 0x03]);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunSMovRRP() => ExecuteTest();

    [IterationSetup(Target = "RunSMovRIP")]
    public void SetupSMovRIP() => Setup([0x09, 0x02, 0x00, 0x01, 0x00, 0x00]);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunSMovRIP() => ExecuteTest();

    [IterationSetup(Target = "RunSMovRPR")]
    public void SetupSMovRPR() => Setup([0x0A, 0x03, 0x01]);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunSMovRPR() => ExecuteTest();

    [IterationSetup(Target = "RunSMovRPI")]
    public void SetupSMovRPI() => Setup([0x0B, 0x03, 0x05, 0x00]);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunSMovRPI() => ExecuteTest();

    [IterationSetup(Target = "RunSMovIPR")]
    public void SetupSMovIPR() => Setup([0x0C, 0x00, 0x01, 0x00, 0x00, 0x01]);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunSMovIPR() => ExecuteTest();

    [IterationSetup(Target = "RunSMovIPI")]
    public void SetupSMovIPI() => Setup([0x0D, 0x00, 0x01, 0x00, 0x00, 0x05, 0x00]);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunSMovIPI() => ExecuteTest();

    // ---------------- Byte (1 byte) MOV ----------------

    [IterationSetup(Target = "RunBMovRRP")]
    public void SetupBMovRRP() => Setup([0x0E, 0x02, 0x03]);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunBMovRRP() => ExecuteTest();

    [IterationSetup(Target = "RunBMovRIP")]
    public void SetupBMovRIP() => Setup([0x0F, 0x02, 0x00, 0x01, 0x00, 0x00]);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunBMovRIP() => ExecuteTest();

    [IterationSetup(Target = "RunBMovRPR")]
    public void SetupBMovRPR() => Setup([0x10, 0x03, 0x01]);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunBMovRPR() => ExecuteTest();

    [IterationSetup(Target = "RunBMovRPI")]
    public void SetupBMovRPI() => Setup([0x11, 0x03, 0x05]);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunBMovRPI() => ExecuteTest();

    [IterationSetup(Target = "RunBMovIPR")]
    public void SetupBMovIPR() => Setup([0x12, 0x00, 0x01, 0x00, 0x00, 0x01]);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunBMovIPR() => ExecuteTest();

    [IterationSetup(Target = "RunBMovIPI")]
    public void SetupBMovIPI() => Setup([0x13, 0x00, 0x01, 0x00, 0x00, 0x05]);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void RunBMovIPI() => ExecuteTest();
}
