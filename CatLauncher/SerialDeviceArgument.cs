using System.Reflection;

namespace CatLauncher;

public class SerialDeviceArgument(string name, ConstructorInfo constructor) {
    public string Name { get; } = name;
    public ConstructorInfo Constructor { get; } = constructor;
    public Dictionary<string, Argument> Arguments { get; } = [];
    
    public class Argument(object? defaultValue, ArgumentType type) {
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
        CatVm,
        CancellationToken,
    }
}
