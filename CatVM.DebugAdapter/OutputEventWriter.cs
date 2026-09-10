using System.Text;
using CatVM.DebugAdapter.Protocol;

namespace CatVM.DebugAdapter;

/// <summary>
/// Installed as <see cref="Console.Out"/> for the life of the server so that everything which
/// would otherwise print to stdout (guest <c>int 0x80</c> output, device loader messages, fault
/// dumps) reaches the client as <c>output</c> events instead of corrupting the protocol stream.
/// </summary>
public sealed class OutputEventWriter(DapTransport transport) : TextWriter {
    private readonly StringBuilder _line = new();
    private readonly object _lock = new();

    /// <summary>DAP output category: "console" while the adapter is setting up, "stdout" once the guest runs.</summary>
    public string Category { get; set; } = "console";

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value) {
        lock (_lock) {
            _line.Append(value);
            if (value == '\n') FlushLine();
        }
    }

    public override void Write(string? value) {
        if (value == null) return;
        lock (_lock) {
            foreach (char c in value) {
                _line.Append(c);
                if (c == '\n') FlushLine();
            }
        }
    }

    public override void Write(char[] buffer, int index, int count) {
        Write(new string(buffer, index, count));
    }

    public override void Flush() {
        lock (_lock) {
            if (_line.Length != 0) FlushLine();
        }
    }

    private void FlushLine() {
        string text = _line.ToString();
        _line.Clear();
        transport.SendEvent("output", J.Obj(("category", Category), ("output", text)));
    }
}
