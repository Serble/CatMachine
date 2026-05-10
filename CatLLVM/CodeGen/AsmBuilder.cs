using System.Text;

namespace CatLLVM.CodeGen;

/// <summary>
/// Tiny helper for building Cat assembly text. Manages a set of labelled
/// sections (code, rodata, bss) and stitches them together with a small
/// header at output time.
/// </summary>
public sealed class AsmBuilder {
    private readonly StringBuilder _code = new();
    private readonly StringBuilder _rodata = new();
    private readonly StringBuilder _bss = new();

    public StringBuilder Code => _code;
    public StringBuilder Rodata => _rodata;
    public StringBuilder Bss => _bss;

    public void Comment(StringBuilder sb, string text) =>
        sb.AppendLine("; " + text);

    public void Label(StringBuilder sb, string label) =>
        sb.AppendLine(label + ":");

    public void Ins(StringBuilder sb, string ins) =>
        sb.Append("    ").AppendLine(ins);

    public string Build(string sourceName) {
        StringBuilder full = new();
        full.AppendLine("; =========================================");
        full.AppendLine("; Cat assembly produced by CatLLVM");
        full.AppendLine("; Target architecture: 32-bit Cat");
        full.AppendLine($"; Source: {sourceName}");
        full.AppendLine("; =========================================");
        full.AppendLine();
        full.AppendLine("; ---- code ----");
        full.Append(_code);
        if (_rodata.Length > 0) {
            full.AppendLine();
            full.AppendLine("; ---- rodata ----");
            full.Append(_rodata);
        }
        if (_bss.Length > 0) {
            full.AppendLine();
            full.AppendLine("; ---- bss ----");
            full.Append(_bss);
        }
        return full.ToString();
    }
}
