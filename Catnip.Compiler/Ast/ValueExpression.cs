namespace Catnip.Compiler.Ast;

public interface IValueExpression;

public record IntegerLiteral(uint Value) : IValueExpression;

public record StringLiteral(string Value) : IValueExpression;

public record StructSizeof(string StructName) : IValueExpression;

public record StructOffsetOf(string StructName, string ParamName) : IValueExpression;

// Literally a variable value
// eg. a:4 means variable 'a' of size 4 bytes
// public record VariableReference(string Name, int Size) : IValueExpression;

public record VariableToken(string Name) : IValueExpression;

public record BinaryOperation(IValueExpression Left, BinaryOperationType Operator, IValueExpression Right) : IValueExpression;

public record FunctionCall(IValueExpression Target, IValueExpression[] Arguments, FileInformation? FileInformation = null)
    : Statement(FileInformation), IValueExpression;

public record UnaryOperation(UnaryOperationType Operator, IValueExpression Operand) : IValueExpression;

// COMPILE CONSTANTS

public abstract record CompileTimeValue : IValueExpression {
    public static CompileTimeValue From(IValueExpression expr) {
        return expr switch {
            CompileTimeValue ctv => ctv,
            IntegerLiteral il => new CompileTimeNumber(il.Value),
            StructSizeof sso => new CompileTimeStructSize(sso.StructName),
            _ => throw new InvalidOperationException("Cannot convert expression to compile-time value.")
        };
    }
    
    public static bool IsValid(IValueExpression expr) {
        return expr is CompileTimeValue or IntegerLiteral or StructSizeof;
    }

    public uint Resolve(Struct[] structs) {
        switch (this) {
            case CompileTimeNumber ctn:
                return ctn.Value;
            case CompileTimeStructSize cts:
                Struct? structDef = structs.FirstOrDefault(s => s.Name == cts.StructName);
                if (structDef == null) {
                    throw new InvalidOperationException($"Struct '{cts.StructName}' not found when resolving compile-time struct size.");
                }
                return (uint)structDef.Fields.Sum(f => From(f.Size).Resolve(structs));
            default:
                throw new InvalidOperationException("Cannot resolve compile-time value.");
        }
    }
}

public record CompileTimeNumber(uint Value) : CompileTimeValue;

public record CompileTimeStructSize(string StructName) : CompileTimeValue;

public enum BinaryOperationType {
    Add,
    Subtract,
    UnsignedMultiply,
    UnsignedDivide,
    SignedMultiply,
    SignedDivide,
    UnsignedModulus,
    SignedModulus,
    
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    LeftShift,
    RightShift,
    
    LogicalAnd,
    LogicalOr,
    
    Equals,
    NotEquals,
    UnsignedLessThan,
    UnsignedLessThanOrEqual,
    SignedLessThan,
    SignedLessThanOrEqual,
    UnsignedGreaterThan,
    UnsignedGreaterThanOrEqual,
    SignedGreaterThan,
    SignedGreaterThanOrEqual,
    
    Dereference
}

public enum UnaryOperationType {
    Negate,
    BitwiseNot,
    LogicalNot
}

public static class ValueExpressionExtensions {
    extension(BinaryOperation expr) {
        public bool IsMathematical() {
            return expr.Operator is BinaryOperationType.Add or BinaryOperationType.Subtract or
                BinaryOperationType.UnsignedMultiply or BinaryOperationType.UnsignedDivide or
                BinaryOperationType.SignedMultiply or BinaryOperationType.SignedDivide or
                BinaryOperationType.UnsignedModulus or BinaryOperationType.SignedModulus or
                BinaryOperationType.BitwiseAnd or BinaryOperationType.BitwiseOr or
                BinaryOperationType.BitwiseXor or BinaryOperationType.LeftShift or
                BinaryOperationType.RightShift;
        }

        public bool IsComparison() {
            return expr.Operator is BinaryOperationType.Equals or BinaryOperationType.NotEquals or
                BinaryOperationType.UnsignedLessThan or BinaryOperationType.UnsignedLessThanOrEqual or
                BinaryOperationType.UnsignedGreaterThan or BinaryOperationType.UnsignedGreaterThanOrEqual;
        }
        
        public bool IsLogical() {
            return expr.Operator is BinaryOperationType.LogicalAnd or BinaryOperationType.LogicalOr;
        }
    }
}
