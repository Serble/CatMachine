using System.Text.Json;
using System.Text.Json.Nodes;

namespace CatData;

public record DebugTable(DebugSymbol[] Symbols, Dictionary<string, uint> Labels) {

    public string ToJson(JsonSerializerOptions options) {
        return ToJsonNode().ToJsonString(options);
    }

    public JsonNode ToJsonNode() {
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

        return obj;
    }

    public static DebugTable FromJsonNode(JsonNode node) {
        JsonArray symbolsJson = (JsonArray)node[nameof(Symbols)]!;
        DebugSymbol[] symbols = new DebugSymbol[symbolsJson.Count];
        for (int i = 0; i < symbols.Length; i++) {
            symbols[i] = DebugSymbol.FromJsonNode(symbolsJson[i]!);
        }

        JsonObject labelsJson = (JsonObject)node[nameof(Labels)]!;
        Dictionary<string, uint> labels = [];
        foreach ((string key, JsonNode? val) in labelsJson) {
            labels[key] = (uint)val!;
        }

        return new DebugTable(symbols, labels);
    }
}
