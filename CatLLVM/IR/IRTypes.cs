namespace CatLLVM.IR;

// =============================================================================
// LLVM IR AST
// =============================================================================
// A focused subset of LLVM IR. CatVM is 32-bit integer-only, so we model just
// what we can lower: integer types up to 32 bits, pointers, arrays of those,
// and void. No floats, no i64, no structs (yet).

public abstract record IrType {
    /// <summary>Size of one value of this type, in bytes, in the data layout.</summary>
    public abstract int SizeBytes { get; }
    /// <summary>Slot size when the value lives in an SSA register / stack slot. Always 4 except for void.</summary>
    public virtual int SlotBytes => 4;
}

public sealed record IntType(int Bits) : IrType {
    public override int SizeBytes => Bits switch {
        1 or <= 8 => 1,
        <= 16 => 2,
        <= 32 => 4,
        _ => throw new NotSupportedException($"i{Bits} is not supported by CatVM (max i32).")
    };
    public override string ToString() => $"i{Bits}";
}

public sealed record PtrType : IrType {
    public override int SizeBytes => 4;
    public override string ToString() => "ptr";
    public static readonly PtrType Instance = new();
}

public sealed record VoidType : IrType {
    public override int SizeBytes => 0;
    public override int SlotBytes => 0;
    public override string ToString() => "void";
    public static readonly VoidType Instance = new();
}

public sealed record ArrayType(IrType Element, int Count) : IrType {
    public override int SizeBytes => Element.SizeBytes * Count;
    public override string ToString() => $"[{Count} x {Element}]";
}

// -----------------------------------------------------------------------------
// Values (operands)
// -----------------------------------------------------------------------------

public abstract record IrValue(IrType Type);

public sealed record IrConstInt(IrType Ty, long Value) : IrValue(Ty);
public sealed record IrLocalRef(string Name, IrType Ty) : IrValue(Ty);
public sealed record IrGlobalRef(string Name, IrType Ty) : IrValue(Ty);
public sealed record IrUndef(IrType Ty) : IrValue(Ty);
public sealed record IrNull(IrType Ty) : IrValue(Ty);
public sealed record IrConstArray(ArrayType Ty, IReadOnlyList<IrValue> Elements) : IrValue(Ty);
public sealed record IrConstBytes(ArrayType Ty, byte[] Bytes) : IrValue(Ty);
public sealed record IrZeroInit(IrType Ty) : IrValue(Ty);

// -----------------------------------------------------------------------------
// Instructions
// -----------------------------------------------------------------------------

public abstract record IrInstruction {
    public string? Result { get; init; }
    public IrType ResultType { get; init; } = VoidType.Instance;
}

public sealed record AllocaIns(IrType ElementType, int Count) : IrInstruction;
public sealed record LoadIns(IrType LoadType, IrValue Pointer) : IrInstruction;
public sealed record StoreIns(IrValue Value, IrValue Pointer) : IrInstruction;

public enum BinOp { Add, Sub, Mul, SDiv, UDiv, SRem, URem, And, Or, Xor, Shl, LShr, AShr }
public sealed record BinOpIns(BinOp Op, IrValue Lhs, IrValue Rhs) : IrInstruction;

public enum IcmpPred { Eq, Ne, Slt, Sgt, Sle, Sge, Ult, Ugt, Ule, Uge }
public sealed record IcmpIns(IcmpPred Pred, IrValue Lhs, IrValue Rhs) : IrInstruction;

public sealed record BrIns(string Target) : IrInstruction;
public sealed record BrCondIns(IrValue Condition, string IfTrue, string IfFalse) : IrInstruction;
public sealed record RetIns(IrValue? Value) : IrInstruction;
public sealed record CallIns(IrValue Callee, IReadOnlyList<IrValue> Args, IrType ReturnType) : IrInstruction;
public sealed record GepIns(IrType BaseType, IrValue Pointer, IReadOnlyList<IrValue> Indices) : IrInstruction;

public enum CastKind { ZExt, SExt, Trunc, BitCast, PtrToInt, IntToPtr }
public sealed record CastIns(CastKind Kind, IrValue Source, IrType TargetType) : IrInstruction;

public sealed record PhiIns(IReadOnlyList<(IrValue Value, string FromBlock)> Incoming) : IrInstruction;

// -----------------------------------------------------------------------------
// Blocks, functions, module
// -----------------------------------------------------------------------------

public sealed class IrBasicBlock {
    public string Label { get; }
    public List<IrInstruction> Instructions { get; } = [];
    public IrBasicBlock(string label) { Label = label; }
}

public sealed record IrParam(string Name, IrType Ty);

public sealed class IrFunction {
    public string Name { get; }
    public IrType ReturnType { get; }
    public List<IrParam> Params { get; } = [];
    public List<IrBasicBlock> Blocks { get; } = [];
    public bool IsDeclaration => Blocks.Count == 0;
    public IrFunction(string name, IrType ret) { Name = name; ReturnType = ret; }
}

public sealed class IrGlobalDecl {
    public string Name { get; }
    public IrType Ty { get; }
    public IrValue? Initializer { get; }
    public bool IsConstant { get; }
    public IrGlobalDecl(string name, IrType ty, IrValue? init, bool constant) {
        Name = name; Ty = ty; Initializer = init; IsConstant = constant;
    }
}

public sealed class IrModule {
    public List<IrGlobalDecl> Globals { get; } = [];
    public List<IrFunction> Functions { get; } = [];
}
