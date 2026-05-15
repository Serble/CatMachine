using System.Reflection;

namespace CatLauncher;

public class SerialDeviceArgument(string name, bool register, string[] portValues, ConstructorInfo constructor) {
    public string Name { get; } = name;
    public bool Register { get; } = register;
    public string[] PortValues { get; } = portValues;
    public ConstructorInfo Constructor { get; } = constructor;
    public Dictionary<string, Argument> Arguments { get; } = [];
    
    public class Argument(bool hasDefault, object? defaultValue, ArgumentType type) {
        public bool HasDefault { get; } = hasDefault;
        public object? DefaultValue { get; } = defaultValue;
        public ArgumentType Type { get; } = type;
    }
    
    public enum ArgumentType {
        String,
        SByte,
        Byte,
        UShort,
        Short,
        UInt,
        Int,
        ULong,
        Long,
        Float,
        Double,
        Decimal,
        NullableSByte,
        NullableByte,
        NullableUShort,
        NullableShort,
        NullableUInt,
        NullableInt,
        NullableULong,
        NullableLong,
        NullableFloat,
        NullableDouble,
        NullableDecimal,
        CatVm,
        CancellationToken,
    }
}
