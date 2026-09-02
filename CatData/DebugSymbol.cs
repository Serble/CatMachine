using System.Text.Json.Serialization;
using System.Text.Json.Nodes;

namespace CatData;

/// <summary>
/// Maps one assembled instruction back to the source that produced it.
/// </summary>
/// <param name="FilePos">Byte offset of the instruction in the output ROM.</param>
/// <param name="File">
/// The assembly file the instruction was assembled from. Required to disambiguate
/// <paramref name="Line"/> in projects that use <c>#include</c>, where a bare line number
/// is meaningless on its own.
/// </param>
/// <param name="Line">1-based line number within <paramref name="File"/>.</param>
/// <param name="RawLine">The un-tokenised text of the line that produced the instruction.</param>
/// <param name="SourceFile">
/// The original high-level source file (for example a Catnip <c>.nip</c> file) that this
/// instruction was generated from, or <c>null</c> when the assembly was hand-written and so
/// <paramref name="File"/> is already the original source. Populated from <c>#line</c>
/// directives in the assembly.
/// </param>
/// <param name="SourceLine">
/// 1-based line number within <paramref name="SourceFile"/>. Only meaningful when
/// <paramref name="SourceFile"/> is non-null.
/// </param>
public record DebugSymbol(
    int FilePos,
    string File,
    int Line,
    string RawLine,
    string? SourceFile = null,
    int SourceLine = 0) {

    /// <summary>
    /// The file and line a debugger should show the user: the original high-level source when
    /// this instruction came from one, otherwise the assembly itself.
    /// </summary>
    [JsonIgnore]
    public (string File, int Line) EffectiveLocation => SourceFile != null ? (SourceFile, SourceLine) : (File, Line);

    public JsonNode ToJsonNode() {
        JsonObject obj = new() {
            [nameof(FilePos)] = FilePos,
            [nameof(File)] = File,
            [nameof(Line)] = Line,
            [nameof(RawLine)] = RawLine
        };

        // Only emitted when the instruction actually has a high-level origin, so hand-written
        // assembly keeps producing the same compact debug files it always has.
        if (SourceFile != null) {
            obj[nameof(SourceFile)] = SourceFile;
            obj[nameof(SourceLine)] = SourceLine;
        }

        return obj;
    }

    public static DebugSymbol FromJsonNode(JsonNode node) {
        JsonObject obj = (JsonObject)node;
        int filePos = (int)obj[nameof(FilePos)]!;
        string file = (string)obj[nameof(File)]!;
        int line = (int)obj[nameof(Line)]!;
        string rawLine = (string)obj[nameof(RawLine)]!;
        string? sourceFile = null;
        int sourceLine = 0;

        if (obj.ContainsKey(nameof(SourceFile))) {
            sourceFile = (string)obj[nameof(SourceFile)]!;
            sourceLine = (int)obj[nameof(SourceLine)]!;
        }

        return new DebugSymbol(filePos, file, line, rawLine, sourceFile, sourceLine);
    }
}
