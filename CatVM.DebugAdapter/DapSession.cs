using System.Text.Json.Nodes;
using CatVM.DebugAdapter.Protocol;
using CatVM.Debugger;

namespace CatVM.DebugAdapter;

public enum SessionState {
    Initializing,
    Initialized,
    /// <summary>Launched; waiting for the client to finish sending breakpoints.</summary>
    Configuring,
    Stopped,
    Running,
    Terminated
}

/// <summary>
/// One debug session: request dispatch and the state machine. Requests are handled on the
/// protocol thread; <see cref="OnStopped"/> runs on the VM thread.
/// </summary>
public sealed class DapSession {
    private readonly DapTransport _transport;
    private readonly IVmLauncher _launcher;
    private readonly OutputEventWriter _output;
    private readonly BreakpointStore _breakpoints = new();
    private readonly ExpressionEvaluator _evaluator = new();

    private volatile SessionState _state = SessionState.Initializing;
    private bool _linesStartAt1 = true;
    private bool _configurationDoneSeen;
    private bool _breakOnGuestFault = true;
    private bool _stopOnEntry = true;
    private bool _exitedSent;
    private bool _terminatedSent;
    private bool _shutDown;

    private LaunchedVm? _vm;
    private CatDebugger? _debugger;
    private VmController? _controller;
    private SourceMapper? _mapper;
    private VariablesProvider? _variables;
    private Disassembler? _disassembler;

    public bool ShouldExit { get; private set; }
    public SessionState State => _state;

    public DapSession(DapTransport transport, IVmLauncher launcher, OutputEventWriter output) {
        _transport = transport;
        _launcher = launcher;
        _output = output;
    }

    public void Handle(JsonObject request) {
        if (J.Str(request, "type") != "request") return;
        string command = J.Str(request, "command") ?? string.Empty;
        JsonObject? args = J.Object(request, "arguments");

        try {
            JsonNode? body = Dispatch(command, args, request);
            _transport.SendResponse(request, true, body);
        }
        catch (DapError e) {
            _transport.SendResponse(request, false, message: e.Message);
        }
        catch (EvalException e) {
            _transport.SendResponse(request, false, message: e.Message);
        }
        catch (Exception e) {
            Log.Stderr($"{command} failed: {e}");
            _transport.SendResponse(request, false, message: e.Message);
        }
    }

    /// <summary>Returns the response body; side effects (events, state changes) happen inline.</summary>
    private JsonNode? Dispatch(string command, JsonObject? args, JsonObject request) {
        switch (command) {
            case "initialize":
                return Initialize(args);

            case "launch":
                Launch(args);
                return null;

            case "attach":
                throw new DapError("Attaching to a running VM is not supported; use a launch configuration");

            case "configurationDone":
                _configurationDoneSeen = true;
                if (_state == SessionState.Configuring) StartVm();
                return null;

            case "setBreakpoints":
                return SetBreakpoints(args);

            case "setFunctionBreakpoints":
                return SetFunctionBreakpoints(args);

            case "setInstructionBreakpoints":
                return SetInstructionBreakpoints(args);

            case "setExceptionBreakpoints":
                return SetExceptionBreakpoints(args);

            case "threads":
                return J.Obj(("threads", J.Arr([J.Obj(("id", 1), ("name", "CatVM"))])));

            case "stackTrace": {
                (CatDebugger debugger, SourceMapper mapper) = RequireStopped();
                JsonArray frames = StackTraceBuilder.Build(debugger, mapper,
                    J.Int(args, "startFrame") ?? 0, J.Int(args, "levels") ?? 0, out int total);
                foreach (JsonNode? frame in frames) {
                    if (frame is JsonObject obj && J.Int(obj, "line") is int line && line > 0) obj["line"] = ToClientLine(line);
                }
                return J.Obj(("stackFrames", frames), ("totalFrames", total));
            }

            case "scopes":
                RequireStopped();
                return J.Obj(("scopes", _variables!.Scopes()));

            case "variables":
                RequireStopped();
                return J.Obj(("variables", _variables!.Variables(J.Int(args, "variablesReference") ?? 0)));

            case "setVariable": {
                (CatDebugger debugger, _) = RequireStopped();
                VmEvalContext context = new(debugger.Vm, debugger.Index);
                return _variables!.SetVariable(
                    J.Int(args, "variablesReference") ?? 0,
                    J.Str(args, "name") ?? throw new DapError("setVariable: 'name' is required"),
                    J.Str(args, "value") ?? throw new DapError("setVariable: 'value' is required"),
                    text => _evaluator.Evaluate(text, context).Value);
            }

            case "evaluate":
                return Evaluate(args);

            case "readMemory":
                return ReadMemory(args);

            case "writeMemory":
                return WriteMemory(args);

            case "disassemble":
                return Disassemble(args);

            case "loadedSources":
                return J.Obj(("sources", _mapper?.LoadedSources() ?? new JsonArray()));

            case "source":
                throw new DapError("The adapter never issues source references; open the file by path");

            case "continue":
                Resume(VmCommand.Continue);
                return J.Obj(("allThreadsContinued", true));

            case "next":
                Resume(IsInstructionGranularity(args) ? VmCommand.NextInstruction : VmCommand.NextLine);
                return null;

            case "stepIn":
                Resume(IsInstructionGranularity(args) ? VmCommand.StepInstruction : VmCommand.StepLine);
                return null;

            case "stepOut": {
                (CatDebugger debugger, _) = RequireStopped();
                if (debugger.CallStack.Count == 0) throw new DapError("Not inside a function (no call has been recorded)");
                Resume(VmCommand.StepOut);
                return null;
            }

            case "pause": {
                if (_controller == null) throw new DapError("Not launched");
                if (_state == SessionState.Running) {
                    _controller.RequestPause();
                }
                else if (_state == SessionState.Stopped) {
                    // Already stopped: some clients still expect to hear about it.
                    SendStopped("pause", _debugger!.Vm.Cpu.Ip, null, null);
                }
                return null;
            }

            case "terminate":
                Shutdown();
                SendTerminated();
                return null;

            case "disconnect":
                Shutdown();
                SendExited(0);
                SendTerminated();
                ShouldExit = true;
                return null;

            default:
                throw new DapError($"Unsupported command '{command}'");
        }
    }

    private JsonObject Initialize(JsonObject? args) {
        _linesStartAt1 = J.Bool(args, "linesStartAt1", true);
        _state = SessionState.Initialized;

        return J.Obj(
            ("supportsConfigurationDoneRequest", true),
            ("supportsFunctionBreakpoints", true),
            ("supportsInstructionBreakpoints", true),
            ("supportsSteppingGranularity", true),
            ("supportsReadMemoryRequest", true),
            ("supportsWriteMemoryRequest", true),
            ("supportsDisassembleRequest", true),
            ("supportsSetVariable", true),
            ("supportsEvaluateForHovers", true),
            ("supportsTerminateRequest", true),
            ("supportsLoadedSourcesRequest", true),
            ("supportTerminateDebuggee", true),
            ("supportSuspendDebuggee", false),
            ("supportsRestartRequest", false),
            ("supportsConditionalBreakpoints", false),
            ("supportsCancelRequest", false),
            ("exceptionBreakpointFilters", J.Arr([
                J.Obj(
                    ("filter", "guestFault"),
                    ("label", "Guest CPU faults"),
                    ("description", "Stop when the guest raises a CPU exception (page fault, invalid instruction, divide by zero) even if it has a handler installed."),
                    ("default", true))
            ])));
    }

    private void Launch(JsonObject? args) {
        if (_vm != null) throw new DapError("Already launched");

        LaunchConfig config = LaunchConfig.Parse(args);
        _stopOnEntry = config.StopOnEntry;

        LaunchedVm vm;
        try {
            vm = _launcher.Launch(config);
        }
        catch (ArgumentException e) {
            string message = e.Message.Length == 0 && !config.DisableHardwareManager
                ? "The HardwareManager device was not found. Run the adapter with 'cwd' set to a folder containing hardware/, install devices to the catmachine/hardware application-data folder, or set disableHardwareManager."
                : e.Message;
            throw new DapError("Launch failed: " + message);
        }

        _vm = vm;
        _debugger = new CatDebugger(vm.Vm, vm.DebugTable);
        _mapper = new SourceMapper(_debugger.Index,
            [Directory.GetCurrentDirectory(), Path.GetDirectoryName(Path.GetFullPath(config.Program)), Path.GetDirectoryName(Path.GetFullPath(vm.DebugTablePath))],
            config.AssemblyView);
        _variables = new VariablesProvider(vm.Vm);
        _disassembler = new Disassembler(vm.Vm, _debugger.Index, _mapper);
        _controller = new VmController(_debugger, OnStopped) {
            BreakOnGuestFault = () => _breakOnGuestFault,
            AssemblyView = config.AssemblyView
        };
        _state = SessionState.Configuring;

        Console.WriteLine(vm.HasDebugSymbols
            ? $"Loaded {vm.DebugTable.Symbols.Length} debug symbols and {vm.DebugTable.Labels.Count} labels from {vm.DebugTablePath}"
            : $"No debug symbols found ({vm.DebugTablePath}); only address breakpoints and disassembly will work");

        JsonArray changed = _breakpoints.Attach(_debugger.Index, _mapper);
        ApplyBreakpoints();
        foreach (JsonNode? bp in changed) {
            _transport.SendEvent("breakpoint", J.Obj(("reason", "changed"), ("breakpoint", bp?.DeepClone())));
        }

        _transport.SendEvent("initialized");

        if (_configurationDoneSeen) StartVm();
    }

    private void StartVm() {
        if (_controller == null || _debugger == null) return;
        if (_controller.Started) return;

        _controller.Start();
        _output.Category = "stdout";

        if (_stopOnEntry) {
            _state = SessionState.Stopped;
            SendStopped("entry", _debugger.Vm.Cpu.Ip, null, null);
        }
        else {
            _state = SessionState.Running;
            _controller.Enqueue(VmCommand.Launch);
        }
    }

    private void Resume(VmCommand command) {
        RequireStopped();
        _state = SessionState.Running;
        _transport.SendEvent("continued", J.Obj(("threadId", 1), ("allThreadsContinued", true)));
        _controller!.Enqueue(command);
    }

    /// <summary>Runs on the VM thread.</summary>
    private void OnStopped(StopInfo info) {
        if (info.Kind == StopKind.Shutdown) {
            _state = SessionState.Terminated;
            SendExited(0);
            SendTerminated();
            return;
        }

        _state = SessionState.Stopped;
        switch (info.Kind) {
            case StopKind.Breakpoint:
                SendStopped("breakpoint", info.Ip, null, null, _breakpoints.IdsAt(info.Ip));
                break;

            case StopKind.Step:
                SendStopped("step", info.Ip, null, null);
                break;

            case StopKind.Pause:
                SendStopped("pause", info.Ip, null, null);
                break;

            case StopKind.Halted:
                SendStopped("pause", info.Ip, "VM halted by the guest", info.FaultException?.Message);
                break;

            case StopKind.Exception: {
                string description = $"Guest CPU fault: {info.Fault}" +
                                     (info.FaultHandledByGuest ? " (stopped at the guest's handler)" : " (no guest handler; VM halted)");
                SendStopped("exception", info.Ip, description, info.FaultException?.Message);
                break;
            }

            default:
                SendStopped("entry", info.Ip, null, null);
                break;
        }
    }

    private void SendStopped(string reason, uint ip, string? description, string? text, int[]? hitBreakpointIds = null) {
        JsonObject body = J.Obj(
            ("reason", reason),
            ("threadId", 1),
            ("allThreadsStopped", true),
            ("preserveFocusHint", false));
        if (description != null) body["description"] = description;
        if (text != null) body["text"] = text;
        if (hitBreakpointIds is { Length: > 0 }) body["hitBreakpointIds"] = J.Arr(hitBreakpointIds.Select(id => (JsonNode?)JsonValue.Create(id)));
        _transport.SendEvent("stopped", body);
    }

    private void SendExited(int code) {
        if (_exitedSent) return;
        _exitedSent = true;
        _transport.SendEvent("exited", J.Obj(("exitCode", code)));
    }

    private void SendTerminated() {
        if (_terminatedSent) return;
        _terminatedSent = true;
        _transport.SendEvent("terminated");
    }

    /// <summary>Stop the VM thread and release the machine. Safe to call more than once.</summary>
    public void Shutdown() {
        if (_shutDown) return;
        _shutDown = true;
        _state = SessionState.Terminated;

        _controller?.Terminate(TimeSpan.FromSeconds(2));
        _debugger?.Dispose();
        if (_vm != null) {
            try {
                _vm.Cleanup().GetAwaiter().GetResult();
            }
            catch (Exception e) {
                Log.Stderr("Cleanup failed: " + e.Message);
            }
        }

        _output.Flush();
    }

    private (CatDebugger debugger, SourceMapper mapper) RequireStopped() {
        if (_debugger == null || _mapper == null) throw new DapError("Not launched");
        if (_state == SessionState.Running) throw new DapError("The VM is running; pause it first");
        if (_state == SessionState.Terminated) throw new DapError("The session has ended");
        return (_debugger, _mapper);
    }

    private static bool IsInstructionGranularity(JsonObject? args) {
        return J.Str(args, "granularity") == "instruction";
    }

    private int ToClientLine(int line) => _linesStartAt1 ? line : line - 1;
    private int FromClientLine(int line) => _linesStartAt1 ? line : line + 1;

    private JsonObject SetBreakpoints(JsonObject? args) {
        JsonObject? source = J.Object(args, "source");
        string path = J.Str(source, "path") ?? J.Str(source, "name")
            ?? throw new DapError("setBreakpoints: source.path is required");

        List<(int line, string? condition)> requested = [];
        if (J.Array(args, "breakpoints") is JsonArray breakpoints) {
            foreach (JsonNode? node in breakpoints) {
                JsonObject? bp = node as JsonObject;
                int? line = J.Int(bp, "line");
                if (line == null) continue;
                requested.Add((FromClientLine(line.Value), J.Str(bp, "condition")));
            }
        }
        else if (J.Array(args, "lines") is JsonArray lines) {
            foreach (JsonNode? node in lines) {
                if (node is JsonValue value && value.TryGetValue(out int line)) requested.Add((FromClientLine(line), null));
            }
        }

        JsonArray result = _breakpoints.SetSourceBreakpoints(path, requested);
        ApplyBreakpoints();
        foreach (JsonNode? node in result) {
            if (node is JsonObject obj && J.Int(obj, "line") is int line) obj["line"] = ToClientLine(line);
        }
        return J.Obj(("breakpoints", result));
    }

    private JsonObject SetFunctionBreakpoints(JsonObject? args) {
        List<string> names = [];
        if (J.Array(args, "breakpoints") is JsonArray breakpoints) {
            foreach (JsonNode? node in breakpoints) {
                string? name = J.Str(node as JsonObject, "name");
                if (name != null) names.Add(name);
            }
        }

        JsonArray result = _breakpoints.SetFunctionBreakpoints(names);
        ApplyBreakpoints();
        foreach (JsonNode? node in result) {
            if (node is JsonObject obj && J.Int(obj, "line") is int line) obj["line"] = ToClientLine(line);
        }
        return J.Obj(("breakpoints", result));
    }

    private JsonObject SetInstructionBreakpoints(JsonObject? args) {
        List<(string reference, int offset)> requested = [];
        if (J.Array(args, "breakpoints") is JsonArray breakpoints) {
            foreach (JsonNode? node in breakpoints) {
                JsonObject? bp = node as JsonObject;
                string? reference = J.Str(bp, "instructionReference");
                if (reference == null) continue;
                requested.Add((reference, J.Int(bp, "offset") ?? 0));
            }
        }

        JsonArray result = _breakpoints.SetInstructionBreakpoints(requested);
        ApplyBreakpoints();
        foreach (JsonNode? node in result) {
            if (node is JsonObject obj && J.Int(obj, "line") is int line) obj["line"] = ToClientLine(line);
        }
        return J.Obj(("breakpoints", result));
    }

    private JsonObject? SetExceptionBreakpoints(JsonObject? args) {
        bool enabled = false;
        if (J.Array(args, "filters") is JsonArray filters) {
            enabled |= filters.Any(f => J.AsString(f) == "guestFault");
        }
        if (J.Array(args, "filterOptions") is JsonArray options) {
            enabled |= options.Any(o => J.Str(o as JsonObject, "filterId") == "guestFault");
        }

        _breakOnGuestFault = enabled;
        return null;
    }

    private void ApplyBreakpoints() {
        if (_debugger == null) return;
        HashSet<uint> set = _breakpoints.BuildAddressSet();
        CatDebugger debugger = _debugger;
        if (_controller != null) {
            _controller.RunOnVmThread(() => debugger.Breakpoints = set);
        }
        else {
            debugger.Breakpoints = set;
        }
    }

    private JsonObject Evaluate(JsonObject? args) {
        (CatDebugger debugger, _) = RequireStopped();
        string expression = J.Str(args, "expression") ?? throw new DapError("evaluate: 'expression' is required");
        string context = J.Str(args, "context") ?? "watch";

        EvalResult result = _evaluator.Evaluate(expression, new VmEvalContext(debugger.Vm, debugger.Index));
        string display = result.Display;
        if (context == "repl") {
            string? label = debugger.Index.CodeLabelAt(result.Value);
            if (label != null) display += $"  <{label}>";
        }

        JsonObject body = J.Obj(
            ("result", display),
            ("type", result.Type),
            ("variablesReference", 0));
        if (result.MemoryReference != null) body["memoryReference"] = result.MemoryReference;
        return body;
    }

    private JsonObject ReadMemory(JsonObject? args) {
        (CatDebugger debugger, _) = RequireStopped();
        uint address = (uint)(MemoryReferences.Decode(J.Str(args, "memoryReference")) + (J.Long(args, "offset") ?? 0));
        int count = J.Int(args, "count") ?? 0;
        if (count < 0) throw new DapError("readMemory: count must be non-negative");

        byte[] data = new byte[count];
        int read = 0;
        for (; read < count; read++) {
            try {
                data[read] = debugger.Vm.Read8((uint)(address + read));
            }
            catch (Exception) {
                break;
            }
        }

        JsonObject body = J.Obj(
            ("address", MemoryReferences.Encode(address)),
            ("data", Convert.ToBase64String(data, 0, read)));
        if (read < count) body["unreadableBytes"] = count - read;
        return body;
    }

    private JsonObject WriteMemory(JsonObject? args) {
        (CatDebugger debugger, _) = RequireStopped();
        uint address = (uint)(MemoryReferences.Decode(J.Str(args, "memoryReference")) + (J.Long(args, "offset") ?? 0));
        bool allowPartial = J.Bool(args, "allowPartial", false);
        byte[] data;
        try {
            data = Convert.FromBase64String(J.Str(args, "data") ?? string.Empty);
        }
        catch (FormatException) {
            throw new DapError("writeMemory: data is not valid base64");
        }

        if (!allowPartial && (ulong)address + (ulong)data.Length > (ulong)debugger.Vm.Memory.Length) {
            throw new DapError($"writeMemory: {data.Length} bytes at 0x{address:X8} exceed memory ({debugger.Vm.Memory.Length} bytes)");
        }

        int written = 0;
        for (; written < data.Length; written++) {
            try {
                debugger.Vm.Write8((uint)(address + written), data[written]);
            }
            catch (Exception) when (allowPartial) {
                break;
            }
        }

        return J.Obj(("bytesWritten", written));
    }

    private JsonObject Disassemble(JsonObject? args) {
        RequireStopped();
        uint address = (uint)(MemoryReferences.Decode(J.Str(args, "memoryReference")) + (J.Long(args, "offset") ?? 0));
        JsonArray instructions = _disassembler!.Disassemble(
            address,
            J.Int(args, "instructionOffset") ?? 0,
            J.Int(args, "instructionCount") ?? 0,
            J.Bool(args, "resolveSymbols", true));
        foreach (JsonNode? node in instructions) {
            if (node is JsonObject obj && J.Int(obj, "line") is int line) obj["line"] = ToClientLine(line);
        }
        return J.Obj(("instructions", instructions));
    }
}
