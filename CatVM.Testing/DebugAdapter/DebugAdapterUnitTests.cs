using System.Text.Json.Nodes;
using CatData;
using CatVM.DebugAdapter;
using CatVM.Debugger;
using CatVM.Testing.Dap;

namespace CatVM.Testing.DebugAdapter;

public class ExpressionEvaluatorTests {
    private sealed class FakeContext : IEvalContext {
        public Dictionary<string, uint> Registers { get; } = new() { ["r0"] = 5, ["sp"] = 0x100 };
        public Dictionary<string, uint> Labels { get; } = new() { ["main"] = 0x10, ["SIZE"] = 3 };
        public byte[] Memory { get; } = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();

        public bool TryGetRegister(string name, out uint value) => Registers.TryGetValue(name, out value);
        public bool TryGetLabel(string name, out uint value) => Labels.TryGetValue(name, out value);

        public uint ReadMemory(uint address, int size) {
            if (address + size > Memory.Length) throw new IndexOutOfRangeException("beyond memory");
            return size switch {
                1 => Memory[address],
                2 => (uint)(Memory[address] | Memory[address + 1] << 8),
                _ => (uint)(Memory[address] | Memory[address + 1] << 8 | Memory[address + 2] << 16 | Memory[address + 3] << 24)
            };
        }
    }

    private readonly ExpressionEvaluator _eval = new();
    private readonly FakeContext _ctx = new();

    [TestCase("5", 5u)]
    [TestCase("0x10", 16u)]
    [TestCase("0b101", 5u)]
    [TestCase("r0", 5u)]
    [TestCase("$R0", 5u)]
    [TestCase("main", 0x10u)]
    [TestCase("SIZE", 3u)]
    [TestCase("main + SIZE * 2", 0x16u)]
    [TestCase("(main + SIZE) * 2", 0x26u)]
    [TestCase("-1", 0xFFFFFFFFu)]
    [TestCase("r0 - 6", 0xFFFFFFFFu)]
    [TestCase("[0x10]:1", 0x10u)]
    [TestCase("[0x10]:2", 0x1110u)]
    [TestCase("[main]", 0x13121110u)]
    [TestCase("[[0]:1 + 4]:1", 4u)]
    public void Evaluate_ComputesValue(string expression, uint expected) {
        Assert.That(_eval.Evaluate(expression, _ctx).Value, Is.EqualTo(expected));
    }

    [Test]
    public void Evaluate_SizedDeref_ReportsSizeAndReference() {
        EvalResult r = _eval.Evaluate("[main + 1]:2", _ctx);
        Assert.Multiple(() => {
            Assert.That(r.Size, Is.EqualTo(2));
            Assert.That(r.Type, Is.EqualTo("uint16"));
            Assert.That(r.MemoryReference, Is.EqualTo("0x00000011"));
            Assert.That(r.Display, Is.EqualTo("0x1211 (4625)"));
        });
    }

    [Test]
    public void Evaluate_LabelAndStackPointer_CarryMemoryReference() {
        Assert.Multiple(() => {
            Assert.That(_eval.Evaluate("main", _ctx).MemoryReference, Is.EqualTo("0x00000010"));
            Assert.That(_eval.Evaluate("sp", _ctx).MemoryReference, Is.EqualTo("0x00000100"));
            Assert.That(_eval.Evaluate("r0", _ctx).MemoryReference, Is.Null);
            Assert.That(_eval.Evaluate("main + 1", _ctx).MemoryReference, Is.Null);
        });
    }

    [TestCase("")]
    [TestCase("nosuch")]
    [TestCase("1 +")]
    [TestCase("(1")]
    [TestCase("[1")]
    [TestCase("[1]:3")]
    [TestCase("1 2")]
    [TestCase("[0xFFFFFFF0]")]
    [TestCase("0xZZ")]
    public void Evaluate_BadInput_Throws(string expression) {
        Assert.Throws<EvalException>(() => _eval.Evaluate(expression, _ctx));
    }
}

public class DebugTableIndexTests {
    private static DebugTable Table() {
        return new DebugTable([
            new DebugSymbol(0, "main.cat", 2, "mov r0, 1"),
            new DebugSymbol(6, "main.cat", 3, "mov r1, 2"),
            new DebugSymbol(12, "main.cat", 4, "call f"),
            new DebugSymbol(18, "lib/util.cat", 2, "add r0, r1", "util.nip", 7),
            new DebugSymbol(20, "lib/util.cat", 3, "ret", "util.nip", 7)
        ], new Dictionary<string, uint> {
            ["main"] = 0,
            ["f"] = 18,
            ["SIZE"] = 6   // a #define that happens to equal an instruction address
        });
    }

    [Test]
    public void ExactAndNearestLookups() {
        DebugTableIndex index = new(Table());
        Assert.Multiple(() => {
            Assert.That(index.GetSymbolAt(6)!.RawLine, Is.EqualTo("mov r1, 2"));
            Assert.That(index.GetSymbolAt(7), Is.Null);
            Assert.That(index.IndexAtOrBefore(7), Is.EqualTo(1));
            Assert.That(index.IndexAtOrBefore(0), Is.EqualTo(0));
            Assert.That(index.IndexAtOrBefore(100), Is.EqualTo(4));
            Assert.That(index.InstructionLength(0), Is.EqualTo(6));
            Assert.That(index.InstructionLength(3), Is.EqualTo(2));
            Assert.That(index.InstructionLength(4), Is.EqualTo(-1));
        });
    }

    [Test]
    public void CodeLabels_ExcludeConstants() {
        DebugTableIndex index = new(Table());
        Assert.Multiple(() => {
            Assert.That(index.CodeLabelAt(0), Is.EqualTo("main"));
            Assert.That(index.CodeLabelAt(18), Is.EqualTo("f"));
            Assert.That(index.IsCodeLabel("SIZE"), Is.True, "SIZE's value is an instruction address, so it is indistinguishable from a label");
            Assert.That(index.IsCodeLabel("main"), Is.True);
            Assert.That(index.FunctionContaining(13), Is.EqualTo("SIZE").Or.EqualTo("main"));
            Assert.That(index.FunctionContaining(19), Is.EqualTo("f"));
            Assert.That(index.LabelWithValue(18), Is.EqualTo("f"));
        });
    }

    [Test]
    public void ResolveLine_UsesEffectiveLocationAndDetectsAmbiguity() {
        DebugTableIndex index = new(Table());

        LineResolution byNip = index.ResolveLine(null, 7);
        Assert.Multiple(() => {
            Assert.That(byNip.Matches, Has.Length.EqualTo(2));
            Assert.That(byNip.IsAmbiguous, Is.False);
            Assert.That(byNip.Address, Is.EqualTo(18u), "lowest address on the line");
        });

        LineResolution ambiguous = index.ResolveLine(null, 2, assemblyView: true);
        Assert.Multiple(() => {
            Assert.That(ambiguous.IsAmbiguous, Is.True);
            Assert.That(ambiguous.Files, Is.EquivalentTo(new[] { "main.cat", "lib/util.cat" }));
            Assert.That(ambiguous.Address, Is.Null);
        });

        LineResolution filtered = index.ResolveLine("util.cat", 2, assemblyView: true);
        Assert.That(filtered.Address, Is.EqualTo(18u));

        Assert.That(index.ResolveLine(null, 99).Matches, Is.Empty);
    }

    [Test]
    public void OldDebugFile_WithoutFileField_Loads() {
        JsonNode node = JsonNode.Parse("""{"Symbols":[{"FilePos":0,"Line":1,"RawLine":"nop"}],"Labels":{"a":0}}""")!;
        DebugTable table = DebugTable.FromJsonNode(node);
        Assert.That(table.Symbols[0].File, Is.EqualTo(string.Empty));
    }
}

public class SourceMapperTests {
    private string _dir = null!;

    [SetUp]
    public void SetUp() {
        _dir = Path.Combine(Path.GetTempPath(), "catvm-mapper-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_dir, "lib"));
        File.WriteAllText(Path.Combine(_dir, "main.cat"), "");
        File.WriteAllText(Path.Combine(_dir, "lib", "util.cat"), "");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, true);

    [Test]
    public void ResolvesRelativeSymbolFilesAgainstBaseDirs() {
        DebugTable table = new([
            new DebugSymbol(0, "main.cat", 1, "nop"),
            new DebugSymbol(1, "lib/util.cat", 1, "nop"),
            new DebugSymbol(2, "missing.cat", 1, "nop")
        ], []);
        SourceMapper mapper = new(new DebugTableIndex(table), [_dir], assemblyView: false);

        Assert.Multiple(() => {
            Assert.That(mapper.SourceFor(table.Symbols[0])["path"]!.GetValue<string>(), Is.EqualTo(Path.Combine(_dir, "main.cat")));
            Assert.That(mapper.SourceFor(table.Symbols[1])["path"]!.GetValue<string>(), Is.EqualTo(Path.Combine(_dir, "lib", "util.cat")));
            Assert.That(mapper.SourceFor(table.Symbols[2])["name"]!.GetValue<string>(), Is.EqualTo("missing.cat"));
            Assert.That(mapper.MatchStrength("main.cat", Path.Combine(_dir, "main.cat")), Is.EqualTo(3));
            Assert.That(mapper.MatchStrength("lib/util.cat", Path.Combine("/elsewhere", "lib", "util.cat")), Is.EqualTo(2));
            Assert.That(mapper.MatchStrength("lib/util.cat", Path.Combine("/elsewhere", "util.cat")), Is.EqualTo(1));
            Assert.That(mapper.MatchStrength("lib/util.cat", Path.Combine("/elsewhere", "other.cat")), Is.EqualTo(0));
        });
    }

    [Test]
    public void ResolveClientLine_PrefersStrongestMatch() {
        // Two files called main.cat on the same line: the absolute match wins outright.
        DebugTable table = new([
            new DebugSymbol(0, "main.cat", 1, "nop"),
            new DebugSymbol(1, "other/main.cat", 1, "nop")
        ], []);
        SourceMapper mapper = new(new DebugTableIndex(table), [_dir], assemblyView: false);

        LineResolution exact = mapper.ResolveClientLine(Path.Combine(_dir, "main.cat"), 1);
        Assert.Multiple(() => {
            Assert.That(exact.IsAmbiguous, Is.False);
            Assert.That(exact.Address, Is.EqualTo(0u));
        });

        LineResolution byName = mapper.ResolveClientLine("/somewhere/else/main.cat", 1);
        Assert.That(byName.IsAmbiguous, Is.True, "only bare-name matches: genuinely ambiguous");
    }
}

public class CallStackResyncTests {
    private static CatDebugger Build(string source) {
        (byte[] rom, DebugTable table) = DapFixture.Assemble("t.cat", source);
        CatVm vm = new(64 * 1024, 1_000_000, rom) { Fast = true };
        return new CatDebugger(vm, table);
    }

    [Test]
    public void CallAndRet_PushAndPopFrames() {
        CatDebugger dbg = Build(DapFixture.CallProgram);
        uint callSite = (uint)dbg.DebugTable.Symbols.First(s => s.Line == 4).FilePos;

        dbg.Step(); dbg.Step();   // the two movs
        Assert.That(dbg.CallStack, Is.Empty);

        dbg.Step();               // call
        Assert.That(dbg.CallStack, Has.Count.EqualTo(1));
        CallFrame frame = dbg.CallStack.Peek();
        Assert.Multiple(() => {
            Assert.That(frame.Callee, Is.EqualTo(dbg.DebugTable.Labels["add_regs"]));
            Assert.That(frame.CallSite, Is.EqualTo(callSite));
            Assert.That(frame.ReturnAddress, Is.EqualTo(callSite + 6));
            Assert.That(dbg.Vm.ReadWord(dbg.Vm.Cpu.Sp), Is.EqualTo(callSite + 6), "guest stack agrees");
        });

        dbg.Step();               // add
        dbg.Step();               // ret
        Assert.Multiple(() => {
            Assert.That(dbg.CallStack, Is.Empty);
            Assert.That(dbg.Vm.Cpu.Ip, Is.EqualTo(callSite + 6));
            Assert.That(dbg.Vm.Cpu.R0, Is.EqualTo(3u));
        });
    }

    [Test]
    public void TailJump_ResyncsOnReturn() {
        // main calls a; a jumps (not calls) to b; b returns straight to main.
        const string source =
            "main:\n" +
            "    call a\n" +
            "loop:\n" +
            "    jmp loop\n" +
            "a:\n" +
            "    jmp b\n" +
            "b:\n" +
            "    ret\n";
        CatDebugger dbg = Build(source);
        dbg.Step();  // call a
        dbg.Step();  // jmp b
        Assert.That(dbg.CallStack, Has.Count.EqualTo(1));
        dbg.Step();  // ret
        Assert.Multiple(() => {
            Assert.That(dbg.CallStack, Is.Empty);
            Assert.That(dbg.CallStackUnderflows, Is.EqualTo(0));
            Assert.That(dbg.Vm.Cpu.Ip, Is.EqualTo(6u));
        });
    }

    [Test]
    public void Fault_IsReportedNotThrown() {
        CatDebugger dbg = Build(DapFixture.FaultProgram);
        CatDebugger.StopReason reason = dbg.RunUntil(() => false);
        Assert.Multiple(() => {
            Assert.That(reason, Is.EqualTo(CatDebugger.StopReason.Fault));
            Assert.That(dbg.LastFault, Is.EqualTo(SpecialInterrupts.DivideByZero));
            Assert.That(dbg.FaultHandledByGuest, Is.False);
            Assert.That(dbg.Vm.Paused, Is.True);
        });
    }

    [Test]
    public void StepOver_Fault_OnFirstInstruction_IsReported() {
        CatDebugger dbg = Build(DapFixture.FaultProgram);
        dbg.Step(); dbg.Step();
        Assert.That(dbg.StepOver(), Is.EqualTo(CatDebugger.StopReason.Fault));
    }

    [Test]
    public void Shutdown_StopsRun() {
        CatDebugger dbg = Build(DapFixture.CallProgram);
        int steps = 0;
        CatDebugger.StopReason reason = dbg.RunUntil(() => {
            if (++steps == 3) dbg.Vm.Shutdown();
            return false;
        });
        Assert.Multiple(() => {
            Assert.That(reason, Is.EqualTo(CatDebugger.StopReason.Shutdown));
            Assert.That(dbg.IsShutdown, Is.True);
            Assert.That(dbg.RunUntil(() => false), Is.EqualTo(CatDebugger.StopReason.Shutdown));
        });
    }
}

public class DisassemblerTests {
    [Test]
    public void SymbolSpansAndGaps() {
        (byte[] rom, DebugTable table) = DapFixture.Assemble("t.cat", DapFixture.CallProgram);
        CatVm vm = new(1024, 1_000_000, rom) { Fast = true };
        DebugTableIndex index = new(table);
        Disassembler dis = new(vm, index, new SourceMapper(index, [], false));

        JsonArray all = dis.Disassemble(0, 0, 20, true);
        int symbolCount = index.SymbolsByAddress.Length;
        Assert.Multiple(() => {
            Assert.That(all.Count, Is.EqualTo(20));
            Assert.That(all[0]!["instruction"]!.GetValue<string>(), Is.EqualTo("mov r0, 1"));
            Assert.That(all[0]!["symbol"]!.GetValue<string>(), Is.EqualTo("main"));
            Assert.That(all[0]!["location"], Is.Not.Null);
            Assert.That(all[1]!["location"], Is.Null, "same file as the previous entry: omitted");
            Assert.That(all[1]!["line"]!.GetValue<int>(), Is.EqualTo(3));
            Assert.That(all[symbolCount - 1]!["instruction"]!.GetValue<string>(), Is.EqualTo("ret"));
            // Past the last symbol the bytes are zero-filled memory shown one per entry.
            Assert.That(all[symbolCount + 3]!["instruction"]!.GetValue<string>(), Does.EndWith("?").Or.StartWith("db"));
        });

        JsonArray back = dis.Disassemble(index.SymbolsByAddress[2].FilePos > 0 ? (uint)index.SymbolsByAddress[2].FilePos : 0, -5, 3, false);
        Assert.That(back[0]!["address"]!.GetValue<string>(), Is.EqualTo("0x00000000"), "cannot go before address 0");
    }
}
