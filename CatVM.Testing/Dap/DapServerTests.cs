using System.Text.Json;

namespace CatVM.Testing.Dap;

/// <summary>
/// End-to-end tests against <c>catlaunch dap</c> running as a separate process, speaking the
/// wire protocol exactly as VS Code or nvim-dap would.
/// </summary>
[TestFixture]
[NonParallelizable]
public class DapServerTests {
    private DapFixture _fixture = null!;
    private DapTestClient _client = null!;

    private object LaunchArgs(bool stopOnEntry = true, string? sourceView = null, bool testInts = false) => new {
        program = _fixture.RomPath,
        disableHardwareManager = true,
        fast = true,
        stopOnEntry,
        sourceView,
        testInts
    };

    private void Start(string source, string fileName = "fixture.cat") {
        _fixture = new DapFixture(fileName, source);
        _client = new DapTestClient(_fixture.Directory);
        _client.Initialize();
    }

    [TearDown]
    public void TearDown() {
        _client?.Dispose();
        _fixture?.Dispose();
    }

    private static uint Ip(JsonElement stackTraceBody) {
        string reference = stackTraceBody.GetProperty("stackFrames")[0].GetProperty("instructionPointerReference").GetString()!;
        return Convert.ToUInt32(reference[2..], 16);
    }

    private JsonElement StackTrace() => _client.Request("stackTrace", new { threadId = 1 });

    private JsonElement SetLineBreakpoints(params int[] lines) {
        return _client.Request("setBreakpoints", new {
            source = new { path = _fixture.SourcePath },
            breakpoints = lines.Select(l => new { line = l }).ToArray()
        }).GetProperty("breakpoints");
    }

    [Test]
    public void Initialize_AdvertisesCapabilities() {
        _fixture = new DapFixture("fixture.cat", DapFixture.CallProgram);
        _client = new DapTestClient(_fixture.Directory);
        JsonElement caps = _client.Initialize();

        Assert.Multiple(() => {
            Assert.That(caps.GetProperty("supportsConfigurationDoneRequest").GetBoolean(), Is.True);
            Assert.That(caps.GetProperty("supportsFunctionBreakpoints").GetBoolean(), Is.True);
            Assert.That(caps.GetProperty("supportsInstructionBreakpoints").GetBoolean(), Is.True);
            Assert.That(caps.GetProperty("supportsSteppingGranularity").GetBoolean(), Is.True);
            Assert.That(caps.GetProperty("supportsReadMemoryRequest").GetBoolean(), Is.True);
            Assert.That(caps.GetProperty("supportsWriteMemoryRequest").GetBoolean(), Is.True);
            Assert.That(caps.GetProperty("supportsDisassembleRequest").GetBoolean(), Is.True);
            Assert.That(caps.GetProperty("supportsSetVariable").GetBoolean(), Is.True);
            Assert.That(caps.GetProperty("exceptionBreakpointFilters")[0].GetProperty("filter").GetString(), Is.EqualTo("guestFault"));
        });
    }

    [Test]
    public void Launch_StopOnEntry_StopsAtAddressZeroInMain() {
        Start(DapFixture.CallProgram);
        _client.Launch(LaunchArgs());

        JsonElement stopped = _client.WaitForEvent("stopped");
        Assert.That(stopped.GetProperty("reason").GetString(), Is.EqualTo("entry"));

        JsonElement trace = StackTrace();
        JsonElement frame = trace.GetProperty("stackFrames")[0];
        Assert.Multiple(() => {
            Assert.That(trace.GetProperty("totalFrames").GetInt32(), Is.EqualTo(1));
            Assert.That(Ip(trace), Is.EqualTo(0u));
            Assert.That(frame.GetProperty("name").GetString(), Is.EqualTo("main"));
            Assert.That(frame.GetProperty("line").GetInt32(), Is.EqualTo(2));
            Assert.That(frame.GetProperty("source").GetProperty("path").GetString(), Is.EqualTo(_fixture.SourcePath));
        });

        JsonElement threads = _client.Request("threads");
        Assert.That(threads.GetProperty("threads")[0].GetProperty("id").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public void Launch_MissingProgram_FailsWithMessage() {
        Start(DapFixture.CallProgram);
        JsonElement response = _client.RequestRaw("launch", new { program = Path.Combine(_fixture.Directory, "nope.bin"), disableHardwareManager = true });
        Assert.Multiple(() => {
            Assert.That(response.GetProperty("success").GetBoolean(), Is.False);
            Assert.That(response.GetProperty("message").GetString(), Does.Contain("ROM not found"));
        });
    }

    [Test]
    public void SetBreakpoints_SourceLine_VerifiedAndHit() {
        Start(DapFixture.CallProgram);
        JsonElement bps = default;
        _client.Launch(LaunchArgs(), () => bps = SetLineBreakpoints(5));
        _client.WaitForEvent("stopped");

        Assert.Multiple(() => {
            Assert.That(bps[0].GetProperty("verified").GetBoolean(), Is.True);
            Assert.That(bps[0].GetProperty("line").GetInt32(), Is.EqualTo(5));
        });
        int id = bps[0].GetProperty("id").GetInt32();

        _client.Request("continue", new { threadId = 1 });
        JsonElement stopped = _client.WaitForEvent("stopped");
        Assert.Multiple(() => {
            Assert.That(stopped.GetProperty("reason").GetString(), Is.EqualTo("breakpoint"));
            Assert.That(stopped.GetProperty("hitBreakpointIds")[0].GetInt32(), Is.EqualTo(id));
            Assert.That(Ip(StackTrace()), Is.EqualTo(_fixture.AddressOfLine(5)));
        });

        // r0 = 1 + 2 by the time we get to line 5.
        JsonElement r0 = _client.Request("evaluate", new { expression = "r0", context = "watch" });
        Assert.That(r0.GetProperty("result").GetString(), Does.StartWith("0x00000003"));
    }

    [Test]
    public void SetBreakpoints_LineWithoutCode_IsUnverified() {
        Start(DapFixture.CallProgram);
        JsonElement bps = default;
        _client.Launch(LaunchArgs(), () => bps = SetLineBreakpoints(1, 42));
        _client.WaitForEvent("stopped");

        Assert.Multiple(() => {
            Assert.That(bps[0].GetProperty("verified").GetBoolean(), Is.False, "a label line has no instruction");
            Assert.That(bps[1].GetProperty("verified").GetBoolean(), Is.False);
            Assert.That(bps[1].GetProperty("message").GetString(), Does.Contain("No code at line 42"));
        });
    }

    [Test]
    public void SetBreakpoints_BeforeLaunch_AreResolvedAtLaunch() {
        Start(DapFixture.CallProgram);
        JsonElement early = SetLineBreakpoints(5);
        Assert.That(early[0].GetProperty("verified").GetBoolean(), Is.False);

        _client.Request("launch", LaunchArgs());
        JsonElement changed = _client.WaitForEvent("breakpoint");
        Assert.Multiple(() => {
            Assert.That(changed.GetProperty("reason").GetString(), Is.EqualTo("changed"));
            Assert.That(changed.GetProperty("breakpoint").GetProperty("verified").GetBoolean(), Is.True);
        });

        _client.WaitForEvent("initialized");
        _client.Request("configurationDone");
        _client.WaitForEvent("stopped");
        _client.Request("continue", new { threadId = 1 });
        Assert.That(_client.WaitForEvent("stopped").GetProperty("reason").GetString(), Is.EqualTo("breakpoint"));
    }

    [Test]
    public void SetFunctionBreakpoints_Label_Hit() {
        Start(DapFixture.CallProgram);
        JsonElement bps = default;
        _client.Launch(LaunchArgs(), () => bps = _client.Request("setFunctionBreakpoints", new {
            breakpoints = new[] { new { name = "add_regs" }, new { name = "nope" } }
        }).GetProperty("breakpoints"));
        _client.WaitForEvent("stopped");

        Assert.Multiple(() => {
            Assert.That(bps[0].GetProperty("verified").GetBoolean(), Is.True);
            Assert.That(bps[0].GetProperty("line").GetInt32(), Is.EqualTo(9));
            Assert.That(bps[1].GetProperty("verified").GetBoolean(), Is.False);
        });

        _client.Request("continue", new { threadId = 1 });
        JsonElement stopped = _client.WaitForEvent("stopped");
        Assert.Multiple(() => {
            Assert.That(stopped.GetProperty("reason").GetString(), Is.EqualTo("breakpoint"));
            Assert.That(Ip(StackTrace()), Is.EqualTo(_fixture.Label("add_regs")));
        });
    }

    [Test]
    public void SetInstructionBreakpoints_Address_Hit() {
        Start(DapFixture.CallProgram);
        uint target = _fixture.AddressOfLine(3);
        _client.Launch(LaunchArgs(), () => {
            JsonElement bps = _client.Request("setInstructionBreakpoints", new {
                breakpoints = new[] { new { instructionReference = $"0x{target:X8}", offset = 0 } }
            }).GetProperty("breakpoints");
            Assert.That(bps[0].GetProperty("verified").GetBoolean(), Is.True);
        });
        _client.WaitForEvent("stopped");

        _client.Request("continue", new { threadId = 1 });
        JsonElement stopped = _client.WaitForEvent("stopped");
        Assert.Multiple(() => {
            Assert.That(stopped.GetProperty("reason").GetString(), Is.EqualTo("breakpoint"));
            Assert.That(Ip(StackTrace()), Is.EqualTo(target));
        });
    }

    [Test]
    public void Next_InstructionGranularity_AdvancesOneInstruction() {
        Start(DapFixture.CallProgram);
        _client.Launch(LaunchArgs());
        _client.WaitForEvent("stopped");

        _client.Request("next", new { threadId = 1, granularity = "instruction" });
        JsonElement stopped = _client.WaitForEvent("stopped");
        Assert.Multiple(() => {
            Assert.That(stopped.GetProperty("reason").GetString(), Is.EqualTo("step"));
            Assert.That(Ip(StackTrace()), Is.EqualTo(_fixture.AddressOfLine(3)));
        });
    }

    [Test]
    public void Next_LineGranularity_StepsOverCall() {
        Start(DapFixture.CallProgram);
        _client.Launch(LaunchArgs(), () => SetLineBreakpoints(4));
        _client.WaitForEvent("stopped");
        _client.Request("continue", new { threadId = 1 });
        _client.WaitForEvent("stopped");
        Assert.That(Ip(StackTrace()), Is.EqualTo(_fixture.AddressOfLine(4)));

        _client.Request("next", new { threadId = 1 });
        JsonElement stopped = _client.WaitForEvent("stopped");
        JsonElement trace = StackTrace();
        Assert.Multiple(() => {
            Assert.That(stopped.GetProperty("reason").GetString(), Is.EqualTo("step"));
            Assert.That(Ip(trace), Is.EqualTo(_fixture.AddressOfLine(5)));
            Assert.That(trace.GetProperty("totalFrames").GetInt32(), Is.EqualTo(1));
        });
    }

    [Test]
    public void StepIn_LineGranularity_EntersFunction_TwoFrames() {
        Start(DapFixture.CallProgram);
        _client.Launch(LaunchArgs(), () => SetLineBreakpoints(4));
        _client.WaitForEvent("stopped");
        _client.Request("continue", new { threadId = 1 });
        _client.WaitForEvent("stopped");

        _client.Request("stepIn", new { threadId = 1 });
        _client.WaitForEvent("stopped");
        JsonElement trace = StackTrace();
        JsonElement frames = trace.GetProperty("stackFrames");
        Assert.Multiple(() => {
            Assert.That(trace.GetProperty("totalFrames").GetInt32(), Is.EqualTo(2));
            Assert.That(frames[0].GetProperty("name").GetString(), Is.EqualTo("add_regs"));
            Assert.That(frames[0].GetProperty("line").GetInt32(), Is.EqualTo(9));
            Assert.That(frames[1].GetProperty("name").GetString(), Is.EqualTo("main"));
            Assert.That(frames[1].GetProperty("line").GetInt32(), Is.EqualTo(4), "the caller frame sits on the call line");
        });

        JsonElement scopes = _client.Request("scopes", new { frameId = 0 }).GetProperty("scopes");
        Assert.That(scopes.GetArrayLength(), Is.EqualTo(3));
    }

    [Test]
    public void StepOut_ReturnsToCaller() {
        Start(DapFixture.CallProgram);
        _client.Launch(LaunchArgs(), () => _client.Request("setFunctionBreakpoints", new { breakpoints = new[] { new { name = "add_regs" } } }));
        _client.WaitForEvent("stopped");
        _client.Request("continue", new { threadId = 1 });
        _client.WaitForEvent("stopped");

        _client.Request("stepOut", new { threadId = 1 });
        JsonElement stopped = _client.WaitForEvent("stopped");
        JsonElement trace = StackTrace();
        Assert.Multiple(() => {
            Assert.That(stopped.GetProperty("reason").GetString(), Is.EqualTo("step"));
            Assert.That(Ip(trace), Is.EqualTo(_fixture.AddressOfLine(5)));
            Assert.That(trace.GetProperty("totalFrames").GetInt32(), Is.EqualTo(1));
        });

        // Not in a function any more: stepOut must refuse rather than run away.
        JsonElement refused = _client.RequestRaw("stepOut", new { threadId = 1 });
        Assert.That(refused.GetProperty("success").GetBoolean(), Is.False);
    }

    [Test]
    public void Pause_WhileSpinning_EmitsStoppedPause() {
        Start(DapFixture.CallProgram);
        _client.Launch(LaunchArgs(stopOnEntry: false));

        // The program runs straight into `loop: jmp loop`; give it a moment then pause it.
        Thread.Sleep(200);
        JsonElement running = _client.RequestRaw("stackTrace", new { threadId = 1 });
        Assert.That(running.GetProperty("success").GetBoolean(), Is.False, "state reads are refused while running");

        _client.Request("pause", new { threadId = 1 });
        JsonElement stopped = _client.WaitForEvent("stopped");
        Assert.Multiple(() => {
            Assert.That(stopped.GetProperty("reason").GetString(), Is.EqualTo("pause"));
            Assert.That(Ip(StackTrace()), Is.EqualTo(_fixture.AddressOfLine(7)));
        });
    }

    [Test]
    public void Variables_RegistersAndFlags_ReadAndWrite() {
        Start(DapFixture.CallProgram);
        _client.Launch(LaunchArgs(), () => SetLineBreakpoints(5));
        _client.WaitForEvent("stopped");
        _client.Request("continue", new { threadId = 1 });
        _client.WaitForEvent("stopped");

        JsonElement registers = _client.Request("variables", new { variablesReference = 1 }).GetProperty("variables");
        Dictionary<string, string> values = registers.EnumerateArray()
            .ToDictionary(v => v.GetProperty("name").GetString()!, v => v.GetProperty("value").GetString()!);
        Assert.Multiple(() => {
            Assert.That(values["r0"], Is.EqualTo("0x00000003 (3)"));
            Assert.That(values["r1"], Is.EqualTo("0x00000002 (2)"));
            Assert.That(values["ip"], Is.EqualTo($"0x{_fixture.AddressOfLine(5):X8} ({_fixture.AddressOfLine(5)})"));
        });

        JsonElement set = _client.Request("setVariable", new { variablesReference = 1, name = "r5", value = "0x10 + 6" });
        Assert.That(set.GetProperty("value").GetString(), Is.EqualTo("0x00000016 (22)"));
        Assert.That(_client.Request("evaluate", new { expression = "r5" }).GetProperty("result").GetString(), Is.EqualTo("0x00000016 (22)"));

        _client.Request("setVariable", new { variablesReference = 2, name = "carry", value = "true" });
        JsonElement flags = _client.Request("variables", new { variablesReference = 2 }).GetProperty("variables");
        Assert.That(flags.EnumerateArray().First(f => f.GetProperty("name").GetString() == "carry").GetProperty("value").GetString(), Is.EqualTo("true"));

        JsonElement cpu = _client.Request("variables", new { variablesReference = 3 }).GetProperty("variables");
        Assert.That(cpu.EnumerateArray().Any(v => v.GetProperty("name").GetString() == "mode"), Is.True);
    }

    [Test]
    public void Evaluate_LabelsDerefsAndArithmetic() {
        Start(DapFixture.CallProgram);
        _client.Launch(LaunchArgs());
        _client.WaitForEvent("stopped");

        uint addRegs = _fixture.Label("add_regs");
        Assert.Multiple(() => {
            Assert.That(_client.Request("evaluate", new { expression = "add_regs" }).GetProperty("result").GetString(), Is.EqualTo($"0x{addRegs:X8} ({addRegs})"));
            Assert.That(_client.Request("evaluate", new { expression = "add_regs + 2" }).GetProperty("result").GetString(), Is.EqualTo($"0x{addRegs + 2:X8} ({addRegs + 2})"));
            Assert.That(_client.Request("evaluate", new { expression = "[0]:1" }).GetProperty("result").GetString(), Is.EqualTo($"0x{_fixture.Rom[0]:X2} ({_fixture.Rom[0]})"));
            Assert.That(_client.Request("evaluate", new { expression = "[main + 2]" }).GetProperty("result").GetString(), Is.EqualTo("0x00000001 (1)"), "immediate operand of `mov r0, 1`");
            Assert.That(_client.Request("evaluate", new { expression = "add_regs", context = "repl" }).GetProperty("result").GetString(), Does.Contain("<add_regs>"));
        });

        JsonElement bad = _client.RequestRaw("evaluate", new { expression = "nosuchthing" });
        Assert.That(bad.GetProperty("success").GetBoolean(), Is.False);
    }

    [Test]
    public void ReadMemory_WriteMemory_RoundTrip() {
        Start(DapFixture.CallProgram);
        _client.Launch(LaunchArgs());
        _client.WaitForEvent("stopped");

        JsonElement read = _client.Request("readMemory", new { memoryReference = "0x0", count = 8 });
        byte[] data = Convert.FromBase64String(read.GetProperty("data").GetString()!);
        Assert.That(data, Is.EqualTo(_fixture.Rom.Take(8).ToArray()));

        byte[] payload = [0xDE, 0xAD, 0xBE, 0xEF];
        JsonElement write = _client.Request("writeMemory", new { memoryReference = "0x1000", data = Convert.ToBase64String(payload) });
        Assert.That(write.GetProperty("bytesWritten").GetInt32(), Is.EqualTo(4));

        JsonElement back = _client.Request("readMemory", new { memoryReference = "0x1000", offset = 1, count = 3 });
        Assert.That(Convert.FromBase64String(back.GetProperty("data").GetString()!), Is.EqualTo(new byte[] { 0xAD, 0xBE, 0xEF }));

        JsonElement beyond = _client.Request("readMemory", new { memoryReference = "0xFFFFFFF0", count = 32 });
        Assert.That(beyond.GetProperty("unreadableBytes").GetInt32(), Is.EqualTo(32));
    }

    [Test]
    public void Disassemble_ReturnsSourceLinesAndSymbols() {
        Start(DapFixture.CallProgram);
        _client.Launch(LaunchArgs());
        _client.WaitForEvent("stopped");

        JsonElement instructions = _client.Request("disassemble", new {
            memoryReference = "0x0", instructionOffset = 0, instructionCount = 4, resolveSymbols = true
        }).GetProperty("instructions");

        Assert.Multiple(() => {
            Assert.That(instructions.GetArrayLength(), Is.EqualTo(4));
            Assert.That(instructions[0].GetProperty("address").GetString(), Is.EqualTo("0x00000000"));
            Assert.That(instructions[0].GetProperty("instruction").GetString(), Is.EqualTo("mov r0, 1"));
            Assert.That(instructions[0].GetProperty("symbol").GetString(), Is.EqualTo("main"));
            Assert.That(instructions[0].GetProperty("line").GetInt32(), Is.EqualTo(2));
            Assert.That(instructions[2].GetProperty("instruction").GetString(), Is.EqualTo("call add_regs"));
            Assert.That(instructions[2].GetProperty("address").GetString(), Is.EqualTo($"0x{_fixture.AddressOfLine(4):X8}"));
        });

        // Negative offsets walk backwards through the table.
        JsonElement backwards = _client.Request("disassemble", new {
            memoryReference = $"0x{_fixture.AddressOfLine(4):X8}", instructionOffset = -2, instructionCount = 2
        }).GetProperty("instructions");
        Assert.That(backwards[0].GetProperty("address").GetString(), Is.EqualTo("0x00000000"));
    }

    [Test]
    public void GuestFault_EmitsStoppedException() {
        Start(DapFixture.FaultProgram);
        _client.Launch(LaunchArgs(stopOnEntry: false));

        JsonElement stopped = _client.WaitForEvent("stopped");
        Assert.Multiple(() => {
            Assert.That(stopped.GetProperty("reason").GetString(), Is.EqualTo("exception"));
            Assert.That(stopped.GetProperty("description").GetString(), Does.Contain("DivideByZero"));
        });
        // No handler installed: the VM halted at the faulting instruction.
        Assert.That(Ip(StackTrace()), Is.EqualTo(_fixture.AddressOfLine(4)));
    }

    [Test]
    public void GuestFault_FilterOff_StillStopsWhenGuestHasNoHandler() {
        Start(DapFixture.FaultProgram);
        _client.Launch(LaunchArgs(stopOnEntry: false), () => _client.Request("setExceptionBreakpoints", new { filters = Array.Empty<string>() }));

        // With no guest handler the fault halts the machine, so it must still be reported.
        JsonElement stopped = _client.WaitForEvent("stopped");
        Assert.That(stopped.GetProperty("reason").GetString(), Is.EqualTo("exception"));
    }

    [Test]
    public void GuestOutput_BecomesOutputEvents() {
        Start(DapFixture.PrintProgram);
        _client.Launch(LaunchArgs(stopOnEntry: false, testInts: true));

        Thread.Sleep(300);
        _client.Request("pause", new { threadId = 1 });
        _client.WaitForEvent("stopped");

        string output = _client.DrainOutput();
        Assert.That(output, Does.Contain("42 0x0000002a"));
    }

    [Test]
    public void CatnipLineDirective_FramesAndBreakpointsUseHighLevelSource() {
        Start(DapFixture.CatnipMappedProgram, "fixture.asm");
        string nipPath = Path.Combine(_fixture.Directory, "fixture.nip");
        File.WriteAllText(nipPath, string.Join("\n", Enumerable.Range(1, 25).Select(i => $"// line {i}")));

        JsonElement bps = default;
        _client.Launch(LaunchArgs(), () => bps = _client.Request("setBreakpoints", new {
            source = new { path = nipPath },
            breakpoints = new[] { new { line = 12 } }
        }).GetProperty("breakpoints"));
        JsonElement entry = _client.WaitForEvent("stopped");
        Assert.That(entry.GetProperty("reason").GetString(), Is.EqualTo("entry"));

        Assert.Multiple(() => {
            Assert.That(bps[0].GetProperty("verified").GetBoolean(), Is.True);
            Assert.That(bps[0].GetProperty("source").GetProperty("path").GetString(), Is.EqualTo(nipPath));
        });

        JsonElement frame = StackTrace().GetProperty("stackFrames")[0];
        Assert.Multiple(() => {
            Assert.That(frame.GetProperty("line").GetInt32(), Is.EqualTo(10));
            Assert.That(frame.GetProperty("source").GetProperty("path").GetString(), Is.EqualTo(nipPath));
            Assert.That(frame.GetProperty("source").GetProperty("origin").GetString(), Is.EqualTo("catnip"));
            Assert.That(frame.GetProperty("source").GetProperty("sources")[0].GetProperty("path").GetString(), Is.EqualTo(_fixture.SourcePath));
        });

        // Line stepping is by .nip line: both movs on line 10 are one step.
        _client.Request("next", new { threadId = 1 });
        _client.WaitForEvent("stopped");
        Assert.That(StackTrace().GetProperty("stackFrames")[0].GetProperty("line").GetInt32(), Is.EqualTo(11));

        _client.Request("continue", new { threadId = 1 });
        JsonElement stopped = _client.WaitForEvent("stopped");
        Assert.Multiple(() => {
            Assert.That(stopped.GetProperty("reason").GetString(), Is.EqualTo("breakpoint"));
            Assert.That(StackTrace().GetProperty("stackFrames")[0].GetProperty("line").GetInt32(), Is.EqualTo(12));
        });
    }

    [Test]
    public void CatnipLineDirective_AssemblyView_UsesAssemblyLines() {
        Start(DapFixture.CatnipMappedProgram, "fixture.asm");
        _client.Launch(LaunchArgs(sourceView: "assembly"), () => SetLineBreakpoints(8));
        _client.WaitForEvent("stopped");

        JsonElement frame = StackTrace().GetProperty("stackFrames")[0];
        Assert.Multiple(() => {
            Assert.That(frame.GetProperty("line").GetInt32(), Is.EqualTo(3));
            Assert.That(frame.GetProperty("source").GetProperty("path").GetString(), Is.EqualTo(_fixture.SourcePath));
        });

        _client.Request("next", new { threadId = 1 });
        _client.WaitForEvent("stopped");
        Assert.That(StackTrace().GetProperty("stackFrames")[0].GetProperty("line").GetInt32(), Is.EqualTo(4), "assembly lines step one at a time");

        _client.Request("continue", new { threadId = 1 });
        _client.WaitForEvent("stopped");
        Assert.That(StackTrace().GetProperty("stackFrames")[0].GetProperty("line").GetInt32(), Is.EqualTo(8));
    }

    [Test]
    public void Terminate_ThenDisconnect_ExitsProcess() {
        Start(DapFixture.CallProgram);
        _client.Launch(LaunchArgs(stopOnEntry: false));
        Thread.Sleep(100);

        _client.Request("terminate");
        _client.WaitForEvent("terminated");

        _client.Request("disconnect");
        Assert.That(_client.WaitForExit(10000), Is.True, "adapter process should exit after disconnect");
    }
}
