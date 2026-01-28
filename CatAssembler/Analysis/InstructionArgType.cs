using CatData;

namespace CatAssembler.Analysis;

public interface IInstructionArgType {
    int SizeInBytes { get; }
    byte[] GetBytes();
}

public static class InstructionArgTypes {
    public static readonly IInstructionArgType Immediate32 = new Immediate32Type();
    public static readonly IInstructionArgType Immediate16 = new Immediate16Type();
    public static readonly IInstructionArgType Immediate8 = new Immediate8Type();
    public static readonly IInstructionArgType Register = new RegisterType(CatData.Register.R0);
}

public abstract record ImmediateType : IInstructionArgType {
    public abstract int SizeInBytes { get; }
    public abstract byte[] GetBytes();
    public abstract ImmediateType WithValue(uint value);
}

public record Immediate32Type(uint Value = 0) : ImmediateType {
    public override int SizeInBytes => 4;
    
    public override byte[] GetBytes() => BitConverter.GetBytes(Value);
    
    public override ImmediateType WithValue(uint value) => this with { Value = value };
}

public record Immediate16Type(ushort Value = 0) : ImmediateType {
    public override int SizeInBytes => 2;
    
    public override byte[] GetBytes() => BitConverter.GetBytes(Value);
    
    public override ImmediateType WithValue(uint value) => this with { Value = (ushort)value };
}

public record Immediate8Type(byte Value = 0) : ImmediateType {
    public override int SizeInBytes => 1;
    
    public override byte[] GetBytes() => [Value];
    
    public override ImmediateType WithValue(uint value) => this with { Value = (byte)value };
}

public record RegisterType(Register Value) : IInstructionArgType {
    public int SizeInBytes => 1;
    
    public byte[] GetBytes() => [(byte)Value];
}
