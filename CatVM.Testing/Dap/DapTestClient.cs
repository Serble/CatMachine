using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CatVM.Testing.Dap;

/// <summary>
/// Drives <c>catlaunch dap</c> out of process, the way an editor would. The launcher is started
/// directly from its built DLL, because <c>dotnet run</c> writes build output to stdout and
/// would corrupt the protocol stream.
/// </summary>
internal sealed class DapTestClient : IDisposable {
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly Lazy<string> LauncherDll = new(LocateLauncher);

    private readonly Process _process;
    private readonly Stream _stdin;
    private readonly Stream _stdout;
    private readonly StringBuilder _stderr = new();
    private readonly Queue<JsonElement> _events = new();
    private int _nextSeq = 1;

    public string Stderr {
        get {
            lock (_stderr) return _stderr.ToString();
        }
    }

    public DapTestClient(string workingDirectory) {
        ProcessStartInfo psi = new("dotnet", $"\"{LauncherDll.Value}\" dap") {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start catlaunch dap.");
        _stdin = _process.StandardInput.BaseStream;
        _stdout = _process.StandardOutput.BaseStream;
        _process.ErrorDataReceived += (_, e) => {
            if (e.Data == null) return;
            lock (_stderr) _stderr.AppendLine(e.Data);
        };
        _process.BeginErrorReadLine();
    }

    public bool HasExited => _process.HasExited;

    public bool WaitForExit(int timeoutMs) => _process.WaitForExit(timeoutMs);

    public JsonElement Initialize() {
        return Request("initialize", new { clientID = "tests", adapterID = "catvm", linesStartAt1 = true, columnsStartAt1 = true });
    }

    /// <summary>
    /// launch → wait for <c>initialized</c> → optional configuration → configurationDone.
    /// Returns the launch response body.
    /// </summary>
    public JsonElement Launch(object arguments, Action? configure = null) {
        JsonElement launch = Request("launch", arguments);
        WaitForEvent("initialized");
        configure?.Invoke();
        Request("configurationDone", new { });
        return launch;
    }

    /// <summary>Sends a request and returns its body; throws when the adapter reports failure.</summary>
    public JsonElement Request(string command, object? arguments = null, int timeoutMs = 15000) {
        JsonElement response = RequestRaw(command, arguments, timeoutMs);
        if (!response.GetProperty("success").GetBoolean()) {
            string message = response.TryGetProperty("message", out JsonElement m) ? m.GetString() ?? "" : "";
            throw new InvalidOperationException($"DAP request '{command}' failed: {message}");
        }

        return response.TryGetProperty("body", out JsonElement body) ? body : default;
    }

    /// <summary>Sends a request and returns the whole response, success or not.</summary>
    public JsonElement RequestRaw(string command, object? arguments = null, int timeoutMs = 15000) {
        int seq = _nextSeq++;
        WriteMessage(new { seq, type = "request", command, arguments = arguments ?? new { } });

        DateTime end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < end) {
            JsonElement message = ReadMessage();
            string type = message.GetProperty("type").GetString() ?? "";
            if (type == "event") {
                _events.Enqueue(message);
                continue;
            }

            if (type == "response" && message.GetProperty("request_seq").GetInt32() == seq) {
                return message;
            }
        }

        throw new TimeoutException($"Timed out waiting for response to '{command}'. stderr:\n{Stderr}");
    }

    /// <summary>Returns the body of the next event named <paramref name="name"/>, queued or not yet read.</summary>
    public JsonElement WaitForEvent(string name, int timeoutMs = 15000) {
        return WaitForEventMessage(name, timeoutMs).TryGetProperty("body", out JsonElement body) ? body : default;
    }

    public JsonElement WaitForEventMessage(string name, int timeoutMs = 15000) {
        int count = _events.Count;
        for (int i = 0; i < count; i++) {
            JsonElement queued = _events.Dequeue();
            if (queued.GetProperty("event").GetString() == name) return queued;
            _events.Enqueue(queued);
        }

        DateTime end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < end) {
            JsonElement message = ReadMessage();
            if (message.GetProperty("type").GetString() != "event") continue;
            if (message.GetProperty("event").GetString() == name) return message;
            _events.Enqueue(message);
        }

        throw new TimeoutException($"Timed out waiting for event '{name}'. stderr:\n{Stderr}");
    }

    /// <summary>Everything received as an `output` event so far, concatenated.</summary>
    public string DrainOutput() {
        StringBuilder sb = new();
        int count = _events.Count;
        for (int i = 0; i < count; i++) {
            JsonElement queued = _events.Dequeue();
            if (queued.GetProperty("event").GetString() == "output") {
                sb.Append(queued.GetProperty("body").GetProperty("output").GetString());
            }
            else {
                _events.Enqueue(queued);
            }
        }

        return sb.ToString();
    }

    public void Dispose() {
        try {
            if (!_process.HasExited) {
                RequestRaw("disconnect", new { }, 5000);
                _process.WaitForExit(5000);
            }
        }
        catch {
            // teardown failures are not interesting
        }
        finally {
            if (!_process.HasExited) {
                _process.Kill(entireProcessTree: true);
            }

            _process.Dispose();
        }
    }

    private JsonElement ReadMessage() {
        int contentLength = -1;
        List<byte> headerBytes = [];

        while (true) {
            int b = _stdout.ReadByte();
            if (b < 0) throw new EndOfStreamException($"Adapter stdout closed while reading headers. stderr:\n{Stderr}");
            headerBytes.Add((byte)b);
            int c = headerBytes.Count;
            if (c >= 4 &&
                headerBytes[c - 4] == '\r' &&
                headerBytes[c - 3] == '\n' &&
                headerBytes[c - 2] == '\r' &&
                headerBytes[c - 1] == '\n') {
                break;
            }
        }

        string headers = Encoding.ASCII.GetString(headerBytes.ToArray());
        foreach (string line in headers.Split(["\r\n"], StringSplitOptions.RemoveEmptyEntries)) {
            const string prefix = "Content-Length:";
            if (!line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (int.TryParse(line[prefix.Length..].Trim(), out int len)) {
                contentLength = len;
                break;
            }
        }

        if (contentLength < 0) throw new InvalidDataException($"Missing Content-Length header in: {headers}");

        byte[] body = new byte[contentLength];
        int read = 0;
        while (read < contentLength) {
            int n = _stdout.Read(body, read, contentLength - read);
            if (n <= 0) throw new EndOfStreamException("Adapter stdout closed while reading body.");
            read += n;
        }

        using JsonDocument doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private void WriteMessage(object message) {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(message);
        byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
        _stdin.Write(header, 0, header.Length);
        _stdin.Write(body, 0, body.Length);
        _stdin.Flush();
    }

    /// <summary>
    /// CatLauncher is built alongside the tests (a ProjectReference with ReferenceOutputAssembly
    /// false), so its DLL is under the same configuration folder as the test assembly.
    /// </summary>
    private static string LocateLauncher() {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        string[] candidates = [
            Path.Combine(RepoRoot, "CatLauncher", "bin", configuration, "net10.0", "CatLauncher.dll"),
            Path.Combine(RepoRoot, "CatLauncher", "bin", "Debug", "net10.0", "CatLauncher.dll"),
            Path.Combine(RepoRoot, "CatLauncher", "bin", "Release", "net10.0", "CatLauncher.dll")
        ];
        return candidates.FirstOrDefault(File.Exists)
               ?? throw new FileNotFoundException("CatLauncher.dll not found; build CatLauncher first", candidates[0]);
    }

    private static string FindRepoRoot() {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir != null) {
            if (File.Exists(Path.Combine(dir.FullName, "CatMachine.sln"))) return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}
