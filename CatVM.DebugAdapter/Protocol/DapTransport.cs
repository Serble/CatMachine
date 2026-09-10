using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CatVM.DebugAdapter.Protocol;

/// <summary>
/// Debug Adapter Protocol framing over a pair of streams: <c>Content-Length</c> headers, one JSON
/// message per body. Reads happen on one thread; writes may come from any thread (the VM thread
/// sends <c>stopped</c> and <c>output</c> events) and are serialised with a lock.
/// </summary>
public sealed class DapTransport(Stream input, Stream output) {
    private readonly object _writeLock = new();
    private int _seq;

    /// <summary>
    /// Reads the next message. Returns false at end of stream. A body that is not a JSON object
    /// yields <c>null</c> so the caller can skip it rather than dying.
    /// </summary>
    public bool TryRead(out JsonObject? message) {
        message = null;
        int contentLength = -1;

        List<byte> headerBytes = [];
        while (true) {
            int value = input.ReadByte();
            if (value < 0) return false;
            headerBytes.Add((byte)value);

            int count = headerBytes.Count;
            bool hasCrlfTerminator = count >= 4 &&
                headerBytes[count - 4] == '\r' &&
                headerBytes[count - 3] == '\n' &&
                headerBytes[count - 2] == '\r' &&
                headerBytes[count - 1] == '\n';
            bool hasLfTerminator = count >= 2 &&
                headerBytes[count - 2] == '\n' &&
                headerBytes[count - 1] == '\n';
            if (hasCrlfTerminator || hasLfTerminator) {
                break;
            }
        }

        string headers = Encoding.ASCII.GetString(headerBytes.ToArray())
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (string headerLine in headers.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
            const string contentLengthPrefix = "Content-Length:";
            if (!headerLine.StartsWith(contentLengthPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            if (int.TryParse(headerLine[contentLengthPrefix.Length..].Trim(), out int parsedLength)) {
                contentLength = parsedLength;
                break;
            }
        }

        if (contentLength < 0) return false;

        byte[] buffer = new byte[contentLength];
        int read = 0;
        while (read < contentLength) {
            int chunk = input.Read(buffer, read, contentLength - read);
            if (chunk <= 0) return false;
            read += chunk;
        }

        string payload = Encoding.UTF8.GetString(buffer);
        if (Log.Verbose) Log.Stderr("<- " + payload);

        try {
            message = JsonNode.Parse(payload) as JsonObject;
        }
        catch (JsonException e) {
            Log.Stderr("Discarding unparseable message: " + e.Message);
        }

        return true;
    }

    public void SendResponse(JsonObject request, bool success, JsonNode? body = null, string? message = null) {
        JsonObject response = new() {
            ["type"] = "response",
            ["request_seq"] = J.Int(request, "seq") ?? 0,
            ["success"] = success,
            ["command"] = J.Str(request, "command") ?? string.Empty
        };
        if (message != null) response["message"] = message;
        if (body != null) response["body"] = body;
        Write(response);
    }

    public void SendEvent(string name, JsonNode? body = null) {
        JsonObject evt = new() {
            ["type"] = "event",
            ["event"] = name
        };
        if (body != null) evt["body"] = body;
        Write(evt);
    }

    private void Write(JsonObject message) {
        lock (_writeLock) {
            message["seq"] = Interlocked.Increment(ref _seq);
            string payload = message.ToJsonString();
            if (Log.Verbose) Log.Stderr("-> " + payload);
            byte[] body = Encoding.UTF8.GetBytes(payload);
            byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
            output.Write(header, 0, header.Length);
            output.Write(body, 0, body.Length);
            output.Flush();
        }
    }
}
