using System.Text.Json.Nodes;
using CatData;
using CatVM.DebugAdapter.Protocol;

namespace CatVM.DebugAdapter;

/// <summary>
/// The scopes a client can expand. The machine has no locals, so every scope is global and
/// its <c>variablesReference</c> is a fixed number.
/// </summary>
public sealed class VariablesProvider(CatVm vm) {
    public const int RegistersScope = 1;
    public const int FlagsScope = 2;
    public const int CpuScope = 3;

    private static readonly string[] RegisterNames = ["r0", "r1", "r2", "r3", "r4", "r5", "r6", "r7", "sp", "ip", "fl"];
    private static readonly string[] FlagNames = ["zero", "carry", "sign", "overflow"];

    public JsonArray Scopes() {
        return J.Arr([
            J.Obj(("name", "Registers"), ("presentationHint", "registers"), ("variablesReference", RegistersScope), ("expensive", false)),
            J.Obj(("name", "Flags"), ("variablesReference", FlagsScope), ("expensive", false)),
            J.Obj(("name", "CPU"), ("variablesReference", CpuScope), ("expensive", false))
        ]);
    }

    public JsonArray Variables(int reference) {
        return reference switch {
            RegistersScope => Registers(),
            FlagsScope => Flags(),
            CpuScope => Cpu(),
            _ => throw new DapError($"Unknown variablesReference {reference}")
        };
    }

    private JsonArray Registers() {
        List<JsonNode?> vars = [];
        for (byte i = 0; i < RegisterNames.Length; i++) {
            uint value = vm.Cpu.Get(i);
            JsonObject var = J.Obj(
                ("name", RegisterNames[i]),
                ("value", FormatWord(value)),
                ("type", "uint32"),
                ("evaluateName", RegisterNames[i]),
                ("variablesReference", i == (byte)Register.Fl ? FlagsScope : 0));
            if (i is (byte)Register.Sp or (byte)Register.Ip) {
                var["memoryReference"] = MemoryReferences.Encode(value);
            }
            vars.Add(var);
        }

        return J.Arr(vars);
    }

    private JsonArray Flags() {
        CatCpuState cpu = vm.Cpu;
        bool[] values = [cpu.ZeroFlag, cpu.CarryFlag, cpu.SignFlag, cpu.OverflowFlag];
        return J.Arr(FlagNames.Select((name, i) => (JsonNode?)J.Obj(
            ("name", name),
            ("value", values[i] ? "true" : "false"),
            ("type", "bool"),
            ("variablesReference", 0))));
    }

    private JsonArray Cpu() {
        CatCpuState cpu = vm.Cpu;
        string modeName = cpu.Mode switch {
            0b00 => "kernel",
            0b01 => "user",
            0b10 => "supervisor",
            0b11 => "driver",
            _ => "?"
        };

        return J.Arr([
            ReadOnly("mode", $"{modeName} (0b{Convert.ToString(cpu.Mode, 2).PadLeft(2, '0')})", "string"),
            ReadOnly("virtualMode", cpu.VirtualMode ? "true" : "false", "bool"),
            ReadOnly("supervisorMode", cpu.SupervisorMode ? "true" : "false", "bool"),
            ReadOnly("it", FormatWord(cpu.It), "uint32", MemoryReferences.Encode(cpu.It)),
            ReadOnly("ksp", FormatWord(cpu.Ksp), "uint32", MemoryReferences.Encode(cpu.Ksp)),
            ReadOnly("mbase", FormatWord(cpu.MBase), "uint32"),
            ReadOnly("mlen", FormatWord(cpu.MLen), "uint32"),
            ReadOnly("ticks", vm.TicksPassed.ToString(), "int64"),
            ReadOnly("memorySize", vm.Memory.Length.ToString(), "int32")
        ]);
    }

    private static JsonObject ReadOnly(string name, string value, string type, string? memoryReference = null) {
        JsonObject var = J.Obj(
            ("name", name),
            ("value", value),
            ("type", type),
            ("variablesReference", 0),
            ("presentationHint", J.Obj(("attributes", J.Strings(["readOnly"])))));
        if (memoryReference != null) var["memoryReference"] = memoryReference;
        return var;
    }

    /// <summary>Applies a <c>setVariable</c>; returns the new value's variable body.</summary>
    public JsonObject SetVariable(int reference, string name, string valueText, Func<string, uint> evaluate) {
        switch (reference) {
            case RegistersScope: {
                int index = Array.IndexOf(RegisterNames, name.ToLowerInvariant());
                if (index < 0) throw new DapError($"Unknown register '{name}'");
                uint value = evaluate(valueText);
                vm.Cpu.Set((byte)index, value);
                JsonObject body = J.Obj(("value", FormatWord(value)), ("type", "uint32"), ("variablesReference", index == (int)Register.Fl ? FlagsScope : 0));
                if (index is (int)Register.Sp or (int)Register.Ip) body["memoryReference"] = MemoryReferences.Encode(value);
                return body;
            }

            case FlagsScope: {
                int index = Array.IndexOf(FlagNames, name.ToLowerInvariant());
                if (index < 0) throw new DapError($"Unknown flag '{name}'");
                bool value = ParseBool(valueText);
                switch (index) {
                    case 0: vm.Cpu.ZeroFlag = value; break;
                    case 1: vm.Cpu.CarryFlag = value; break;
                    case 2: vm.Cpu.SignFlag = value; break;
                    case 3: vm.Cpu.OverflowFlag = value; break;
                }
                return J.Obj(("value", value ? "true" : "false"), ("type", "bool"), ("variablesReference", 0));
            }

            case CpuScope:
                throw new DapError("CPU state variables are read-only");

            default:
                throw new DapError($"Unknown variablesReference {reference}");
        }
    }

    private static bool ParseBool(string text) {
        return text.Trim().ToLowerInvariant() switch {
            "true" or "1" or "yes" or "on" => true,
            "false" or "0" or "no" or "off" => false,
            _ => throw new DapError($"'{text}' is not a boolean")
        };
    }

    public static string FormatWord(uint value) => $"0x{value:X8} ({value})";
}
