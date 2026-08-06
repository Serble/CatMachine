using System.Text.Json.Nodes;

namespace CatData;

public record DebugSymbol(int FilePos, int Line, string RawLine) {

    public JsonNode ToJsonNode() {
        return new JsonObject {
            [nameof(FilePos)] = FilePos,
            [nameof(Line)] = Line,
            [nameof(RawLine)] = RawLine
        };
    }
}
