using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Catnip.Testing.LSP;

internal sealed class LspTestClient : IDisposable {
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string LanguageServerProjectPath =
        Path.Combine(RepoRoot, "Catnip.LanguageServer", "Catnip.LanguageServer.csproj");

    private readonly Process _process;
    private readonly Stream _stdin;
    private readonly Stream _stdout;
    private int _nextId = 1;

    public LspTestClient() {
        ProcessStartInfo psi = new("dotnet", $"run --project \"{LanguageServerProjectPath}\"") {
            WorkingDirectory = RepoRoot,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        _process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start LSP process.");
        _stdin = _process.StandardInput.BaseStream;
        _stdout = _process.StandardOutput.BaseStream;
    }

    public JsonElement Initialize() {
        JsonElement result = SendRequest("initialize", new { });
        SendNotification("initialized", new { });
        return result;
    }

    public JsonElement SendRequest(string method, object? @params) {
        int id = _nextId++;
        WriteMessage(new {
            jsonrpc = "2.0",
            id,
            method,
            @params
        });

        DateTime end = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < end) {
            JsonElement message = ReadMessage();
            if (!message.TryGetProperty("id", out JsonElement msgId)) continue;
            if (msgId.ValueKind != JsonValueKind.Number || msgId.GetInt32() != id) continue;
            if (message.TryGetProperty("error", out JsonElement error)) {
                throw new InvalidOperationException("LSP request failed: " + error.GetRawText());
            }
            return message.GetProperty("result").Clone();
        }

        throw new TimeoutException($"Timed out waiting for response to method '{method}'.");
    }

    public void SendNotification(string method, object? @params) {
        WriteMessage(new {
            jsonrpc = "2.0",
            method,
            @params
        });
    }

    public JsonElement WaitForNotification(string method, int timeoutMs = 15000) {
        DateTime end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < end) {
            JsonElement message = ReadMessage();
            if (!message.TryGetProperty("method", out JsonElement methodElement)) continue;
            if (methodElement.GetString() != method) continue;
            return message.Clone();
        }

        throw new TimeoutException($"Timed out waiting for notification '{method}'.");
    }

    public void Dispose() {
        try {
            try {
                SendRequest("shutdown", new { });
            }
            catch {
                // ignore teardown failures
            }

            SendNotification("exit", new { });
        }
        catch {
            // ignore teardown failures
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
            if (b < 0) throw new EndOfStreamException("LSP server stdout closed while reading headers.");
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

        if (contentLength < 0) throw new InvalidDataException("Missing Content-Length header.");

        byte[] body = new byte[contentLength];
        int read = 0;
        while (read < contentLength) {
            int n = _stdout.Read(body, read, contentLength - read);
            if (n <= 0) throw new EndOfStreamException("LSP server stdout closed while reading body.");
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

    private static string FindRepoRoot() {
        string current = AppContext.BaseDirectory;
        DirectoryInfo? dir = new(current);
        while (dir != null) {
            string sln = Path.Combine(dir.FullName, "CatMachine.sln");
            if (File.Exists(sln)) {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from test base directory.");
    }
}
