using System.Text.Json;
using System.Text.Json.Nodes;

namespace CatData;

public record DebugTable(DebugSymbol[] Symbols, Dictionary<string, uint> Labels) {

    public string ToJson(JsonSerializerOptions options) {
        JsonNode[] symbols = new JsonNode[Symbols.Length];
        for (int i = 0; i < symbols.Length; i++) {
            symbols[i] = Symbols[i].ToJsonNode();
        }

        JsonObject labels = new();
        foreach ((string label, uint val) in Labels) {
            labels[label] = val;
        }

        JsonObject obj = new() {
            [nameof(Symbols)] = new JsonArray(new JsonNodeOptions(), symbols),
            [nameof(Labels)] = labels
        };

        return obj.ToJsonString(options);
    }
}
