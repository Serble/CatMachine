using System.Text.Json.Nodes;

namespace CatVM.DebugAdapter.Protocol;

/// <summary>
/// Hand-built JSON so nothing in the adapter needs reflection-based serialisation (the project
/// is AOT compatible). Every accessor tolerates a missing key or wrong type.
/// </summary>
public static class J {
    public static JsonObject Obj(params (string key, JsonNode? value)[] properties) {
        JsonObject obj = new();
        foreach ((string key, JsonNode? value) in properties) {
            if (value != null) obj[key] = value;
        }

        return obj;
    }

    public static JsonArray Arr(IEnumerable<JsonNode?> items) {
        JsonArray arr = new();
        foreach (JsonNode? item in items) arr.Add(item);
        return arr;
    }

    public static JsonArray Strings(IEnumerable<string> items) {
        return Arr(items.Select(s => (JsonNode?)JsonValue.Create(s)));
    }

    public static string? Str(JsonObject? obj, string key) {
        return obj?[key] is JsonValue value && value.TryGetValue(out string? s) ? s : null;
    }

    public static int? Int(JsonObject? obj, string key) {
        if (obj?[key] is not JsonValue value) return null;
        if (value.TryGetValue(out int i)) return i;
        if (value.TryGetValue(out long l)) return (int)l;
        if (value.TryGetValue(out double d)) return (int)d;
        return null;
    }

    public static long? Long(JsonObject? obj, string key) {
        if (obj?[key] is not JsonValue value) return null;
        if (value.TryGetValue(out long l)) return l;
        if (value.TryGetValue(out int i)) return i;
        if (value.TryGetValue(out double d)) return (long)d;
        return null;
    }

    public static bool Bool(JsonObject? obj, string key, bool defaultValue) {
        return obj?[key] is JsonValue value && value.TryGetValue(out bool b) ? b : defaultValue;
    }

    public static JsonArray? Array(JsonObject? obj, string key) {
        return obj?[key] as JsonArray;
    }

    public static JsonObject? Object(JsonObject? obj, string key) {
        return obj?[key] as JsonObject;
    }

    public static string? AsString(JsonNode? node) {
        return node is JsonValue value && value.TryGetValue(out string? s) ? s : null;
    }
}
