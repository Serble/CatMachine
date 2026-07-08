using CatAssembler.Analysis;
using CatAssembler.Assembler;
using CatAssembler.Parser;
using Catnip.Compiler.CodeGen;
using Catnip.Compiler.Frontend;
using CatVM;
using CatVM.Serial;

namespace Catnip.Testing.Execution;

public static class CatnipProgramRunner {
    public const uint TestOutputPort = 0x0CA7;

    public static CatnipProgramExecutionResult Execute(string source, int maxInstructions = 512) {
        CatnipFrontendService frontend = new();
        string workingDirectory = Directory.GetCurrentDirectory();
        FrontendCompilationResult frontendResult = frontend.AnalyseSource(
            Path.Combine(workingDirectory, "inline-test.nip"),
            source,
            workingDirectory);

        if (!frontendResult.Succeeded || frontendResult.Program == null) {
            string diagnostics = string.Join(
                Environment.NewLine,
                frontendResult.Diagnostics.Select(d => d.Message));
            throw new AssertionException("Catnip compilation failed:" + Environment.NewLine + diagnostics);
        }

        CodeGenerator generator = new(frontendResult.Program);
        string asm = generator.Generate();

        Tokeniser tokeniser = new("inline-test.asm", asm);
        Analyser analyser = new(tokeniser.Tokenise());
        (IOutputSegment[] segments, Dictionary<string, string> constants, _) = analyser.Analyse();

        byte[] rom;
        using (MemoryStream stream = new()) {
            Assembler assembler = new(segments, constants);
            assembler.WriteTo(stream);
            rom = stream.ToArray();
        }

        CatVm vm = new(Math.Max(64 * 1024, rom.Length + 1024), 100_000, rom) { Fast = true };
        List<uint> serialOutput = [];
        vm.RegisterSerialDevice(TestOutputPort, ISerialDevice.Create(
            type: 0xFFFF_FFFE,
            input: _ => uint.MaxValue,
            output: (_, data) => serialOutput.Add(data)));

        TextWriter originalOut = Console.Out;
        using StringWriter capturedOut = new();
        Console.SetOut(capturedOut);
        Exception? runtimeException = null;
        try {
            for (int i = 0; i < maxInstructions && !vm.Paused; i++) {
                try {
                    vm.ExecuteInstruction(fast: true);
                }
                catch (Exception ex) {
                    runtimeException = ex;
                    break;
                }
            }
        }
        finally {
            Console.SetOut(originalOut);
        }

        return new CatnipProgramExecutionResult(capturedOut.ToString(), serialOutput, vm, runtimeException);
    }
}

public sealed record CatnipProgramExecutionResult(
    string ConsoleOutput,
    IReadOnlyList<uint> SerialOutput,
    CatVm Vm,
    Exception? RuntimeException);
