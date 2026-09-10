using System.Text.Json.Nodes;
using CatVM.DebugAdapter.Protocol;

namespace CatVM.DebugAdapter;

/// <summary>One <c>-d</c> device as the launcher understands it: a name and an optional <c>key:value,key:value</c> string.</summary>
public sealed record DeviceSpec(string Name, string? Arguments);

/// <summary>
/// Arguments of the DAP <c>launch</c> request. Mirrors the <c>catlaunch run</c> command line so
/// an IDE's existing run configuration maps onto it one to one.
/// </summary>
public sealed record LaunchConfig(
    string Program,
    string? DebugTable,
    string? Cwd,
    int? Memory,
    int? Ops,
    bool Fast,
    bool TestInts,
    bool DumpErrors,
    bool DisableHardwareManager,
    bool StopOnEntry,
    string SourceView,
    IReadOnlyList<DeviceSpec> Devices,
    IReadOnlyList<string> ExtraArgs) {

    public bool AssemblyView => SourceView.Equals("assembly", StringComparison.OrdinalIgnoreCase);

    public static LaunchConfig Parse(JsonObject? args) {
        string program = J.Str(args, "program") ?? J.Str(args, "rom")
            ?? throw new DapError("launch: 'program' (the path of the ROM to debug) is required");

        List<DeviceSpec> devices = [];
        if (J.Array(args, "devices") is JsonArray deviceArray) {
            foreach (JsonNode? node in deviceArray) {
                switch (node) {
                    case JsonObject deviceObj: {
                        string name = J.Str(deviceObj, "name")
                            ?? throw new DapError("launch: each device object needs a 'name'");
                        string? argString = null;
                        if (J.Object(deviceObj, "args") is JsonObject argObj && argObj.Count != 0) {
                            argString = string.Join(',', argObj.Select(kv => $"{kv.Key}:{J.AsString(kv.Value) ?? kv.Value?.ToJsonString()}"));
                        }
                        else if (J.Str(deviceObj, "args") is string rawArgs) {
                            argString = rawArgs;
                        }
                        devices.Add(new DeviceSpec(name, argString));
                        break;
                    }

                    default: {
                        string? text = J.AsString(node);
                        if (string.IsNullOrWhiteSpace(text)) {
                            throw new DapError("launch: devices must be strings or {name, args} objects");
                        }

                        // "DiskDevice path:disk.img" is how a catide.toml lists a device with
                        // parameters: the first word is the device, the rest its arguments.
                        string[] words = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        devices.Add(new DeviceSpec(words[0], words.Length > 1 ? words[1] : null));
                        break;
                    }
                }
            }
        }

        List<string> extraArgs = [];
        JsonArray? extra = J.Array(args, "extraArgs") ?? J.Array(args, "args");
        if (extra != null) {
            foreach (JsonNode? node in extra) {
                string? text = J.AsString(node);
                if (text != null) extraArgs.Add(text);
            }
        }

        return new LaunchConfig(
            Program: program,
            DebugTable: J.Str(args, "debugTable"),
            Cwd: J.Str(args, "cwd"),
            Memory: J.Int(args, "memory"),
            Ops: J.Int(args, "ops"),
            Fast: J.Bool(args, "fast", false),
            TestInts: J.Bool(args, "testInts", false),
            DumpErrors: J.Bool(args, "dumpErrors", false),
            DisableHardwareManager: J.Bool(args, "disableHardwareManager", false),
            StopOnEntry: J.Bool(args, "stopOnEntry", true),
            SourceView: J.Str(args, "sourceView") ?? "auto",
            Devices: devices,
            ExtraArgs: extraArgs);
    }
}
