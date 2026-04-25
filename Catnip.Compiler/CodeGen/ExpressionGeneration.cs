using System.Diagnostics;
using Catnip.Compiler.Ast;

namespace Catnip.Compiler.CodeGen;

public partial class CodeGenerator {
    
    private (string? value, string? reg, bool preserve) GenerateExprInScratchRegOrGetConst(
        IValueExpression expr, AssemblyFileBuilder file, bool indent, params string[] not) {
        
        (string scratch, bool preserve) = AllocateRegister(not);
        
        AssemblyFileBuilder tempFile = new();
        string? constValue = ResolveExpr(expr, scratch, tempFile, indent);
        if (constValue != null) {
            if (!preserve) FreeRegister(scratch);  // we didn't need the scratch reg after all
            return (constValue, null, false);
        }
        
        // we do need the scratch reg
        // then actually preserve and everything
        if (preserve) {
            file.Push(indent, scratch);
        }
        file.Append(tempFile);
        
        // it's on them to free the register later
        return (null, scratch, preserve);
    }

    private void PlaceExprInReg(IValueExpression expr, string reg, AssemblyFileBuilder file, bool indent) {
        string? constValue = ResolveExpr(expr, reg, file, indent);
        if (constValue != null) {
            file.Append(indent, $"mov {reg}, {constValue}  ; Load compile-time constant into register");
        }
    }
    
    private uint? ResolveNumericalExprOrPlaceInReg(IValueExpression expr, string reg, AssemblyFileBuilder file, bool indent) {
        string? constValue = ResolveExpr(expr, reg, file, indent);
        if (constValue != null) {
            if (uint.TryParse(constValue, out uint val)) {
                return val;
            }
            
            // it's not a number, but a label
            // just put it in the register
            file.Append(indent, $"mov {reg}, {constValue}  ; Load compile-time constant into register");
            return null;
        }

        // it's in the register now
        return null;
    }

    /// <summary>
    /// Resolves an expression and places its value in the specified register.
    /// Or, if it's a compile-time constant, returns the constant as a string instead.
    /// </summary>
    /// <param name="expr">The expression to evaluate.</param>
    /// <param name="reg">The register to place the value in.</param>
    /// <param name="file">The file to append the code to.</param>
    /// <param name="indent">Whether to use indentation when writing.</param>
    /// <returns>The compile time constant string or null.</returns>
    private string? ResolveExpr(IValueExpression expr, string reg, AssemblyFileBuilder file, bool indent) {
        switch (expr) {
            case IntegerLiteral il: {
                return il.Value.ToString();
            }

            case StringLiteral sl: {
                return GetStringLabel(sl.Value);
            }

            case StructSizeof sso: {
                uint size = GetStructSize(sso.StructName);
                return size.ToString();
            }

            case StructOffsetOf soo: {
                Struct structDef = program.Structs.First(s => s.Name == soo.StructName);
                
                // get offset of the member
                int offset = 0;
                bool found = false;
                foreach (VarNameSize member in structDef.Fields) {
                    if (member.Name == soo.ParamName) {
                        found = true;
                    }
                    if (!found) {
                        offset += (int)ResolveCompileConstant(member.Size);
                    }
                }
                
                if (!found) {
                    throw new InvalidOperationException($"Member '{soo.ParamName}' not found in struct '{soo.StructName}'.");
                }

                return offset.ToString();
            }

            case VariableToken vt: {
                Function? function = program.Functions.FirstOrDefault(f => f.Name == vt.Name);
                if (function != null) {
                    return vt.Name;
                }

                if (_localVarOffsets.TryGetValue(vt.Name, out int offset)) {
                    file.Append(indent, 
                        $"mov {reg}, {BasePointerRegister}",
                        $"sub {reg}, {offset}  ; Load address of variable '{vt.Name}'");
                    break;
                }

                if (_globals.Any(g => g.Name == vt.Name) 
                    || program.BinaryGlobals.Any(g => g.Name == vt.Name)) {
                    return vt.Name;
                }

                throw new InvalidOperationException($"Variable or function '{vt.Name}' not found.");
            }

            case BinaryOperation bo: {
                if (bo.Operator == BinaryOperationType.Dereference) {
                    // this doesn't actually use the scratch register
                    // expr:size
                    CompileTimeValue size = CompileTimeValue.From(bo.Right);
                    
                    string? ptrVal = ResolveExpr(bo.Left, reg, file, indent);
                    
                    // the pointer is now in reg
                    file.Append(indent, $"{GetSizedMoveInstruction(size)} {reg}, [{ptrVal ?? reg}]  " +
                                        $"; Dereference pointer with size {ResolveCompileConstant(size)}");
                    break;
                }
                
                
                // reg = Left
                // scratch = Right
                AssemblyFileBuilder leftSetData = new();
                string? constLeftStr = ResolveExpr(bo.Left, reg, leftSetData, indent);
                ConstantIsNumerical(constLeftStr, out uint? constLeft);
                
                // temp because we might not want it if both sides are constant
                AssemblyFileBuilder rightSetData = new();
                (string? value, string? scratch, bool preserve) = GenerateExprInScratchRegOrGetConst(bo.Right, 
                    rightSetData, indent, reg);

                // IS A VALUE 0?
                
                // This means that right is 0
                if (value != null && ConstantIsZero(value)) {
                    switch (bo.Operator) {
                        case BinaryOperationType.Add:
                        case BinaryOperationType.Subtract:
                        case BinaryOperationType.BitwiseOr:
                        case BinaryOperationType.BitwiseXor:
                            // do nothing to the left
                            file.Append(leftSetData);
                            return constLeftStr;
                        
                        case BinaryOperationType.UnsignedMultiply:
                        case BinaryOperationType.SignedMultiply:
                        case BinaryOperationType.LogicalAnd:
                            // multiplying by 0 results in 0
                            // && false also results in 0
                            return "0";
                        
                        // let LogicalOr fall through for simplicity
                        // it needs to return val != 0
                    }
                }
                
                // This means that left is 0
                if (constLeftStr != null && ConstantIsZero(constLeftStr)) {
                    // NOTHING HAS BEEN WRITTEN TO FILE YET
                    switch (bo.Operator) {
                        case BinaryOperationType.Add:
                        case BinaryOperationType.BitwiseOr:
                        case BinaryOperationType.BitwiseXor:
                            // do nothing to the right
                            if (!preserve && scratch != null) {
                                FreeRegister(scratch);
                            }
                            return ResolveExpr(bo.Right, reg, file, indent);
                        
                        case BinaryOperationType.UnsignedMultiply:
                        case BinaryOperationType.SignedMultiply:
                        case BinaryOperationType.LogicalAnd:
                            // multiplying by 0 results in 0
                            // false && anything also results in 0
                            if (!preserve && scratch != null) {
                                FreeRegister(scratch);
                            }
                            return "0";
                        
                        // let LogicalOr fall through for simplicity
                        // it needs to return val != 0
                    }
                }
                
                // IS A VALUE 1?

                // Right is numerical
                if (value != null && ConstantIsNumerical(value, out uint? constRight)) {
                    switch (bo.Operator) {
                        case BinaryOperationType.SignedMultiply:
                        case BinaryOperationType.UnsignedMultiply:
                        case BinaryOperationType.SignedDivide:
                        case BinaryOperationType.UnsignedDivide:
                            if (constRight == 1) {
                                // do nothing to the left
                                file.Append(leftSetData);
                                return constLeftStr;
                            }
                            break;
                        
                        case BinaryOperationType.LogicalOr:
                            if (constRight == 1) {
                                // anything || true is true
                                if (!preserve && scratch != null) {
                                    FreeRegister(scratch);
                                }
                                return "1";
                            }
                            break;
                        
                        // Let LogicalAnd fall through
                        // it needs to do val != 0
                    }
                }
                
                // Left is numerical
                if (constLeft != null) {
                    // divide depends on the order (so doesn't apply here)
                    switch (bo.Operator) {
                        case BinaryOperationType.SignedMultiply:
                        case BinaryOperationType.UnsignedMultiply:
                            if (constLeft == 1) {
                                // do nothing to the right
                                if (!preserve && scratch != null) {
                                    FreeRegister(scratch);
                                }
                                return ResolveExpr(bo.Right, reg, file, indent);
                            }
                            break;
                        
                        case BinaryOperationType.LogicalOr:
                            if (constLeft == 1) {
                                // true || anything is true
                                if (!preserve && scratch != null) {
                                    FreeRegister(scratch);
                                }
                                return "1";
                            }
                            break;
                        
                        // Let LogicalAnd fall through
                        // it needs to do val != 0
                    }
                }

                if (constLeft != null && value != null && ConstantIsNumerical(value, out uint? rightCons)) {
                    if (rightCons == null) {
                        throw new InvalidOperationException("Failed to parse constant numerical value.");
                    }
                    
                    // both sides are constants, we can just compute it now
                    uint result = bo.Operator switch {
                        BinaryOperationType.Add => constLeft.Value + rightCons.Value,
                        BinaryOperationType.Subtract => constLeft.Value - rightCons.Value,
                        BinaryOperationType.UnsignedMultiply => constLeft.Value * rightCons.Value,
                        BinaryOperationType.SignedMultiply => unchecked((uint)((int)constLeft.Value * (int)rightCons.Value)),
                        BinaryOperationType.UnsignedDivide => constLeft.Value / rightCons.Value,
                        BinaryOperationType.SignedDivide => unchecked((uint)((int)constLeft.Value / (int)rightCons.Value)),
                        BinaryOperationType.UnsignedModulus => constLeft.Value % rightCons.Value,
                        BinaryOperationType.SignedModulus => unchecked((uint)((int)constLeft.Value % (int)rightCons.Value)),
                        
                        BinaryOperationType.BitwiseAnd => constLeft.Value & rightCons.Value,
                        BinaryOperationType.BitwiseOr => constLeft.Value | rightCons.Value,
                        BinaryOperationType.BitwiseXor => constLeft.Value ^ rightCons.Value,
                        
                        BinaryOperationType.LogicalAnd => constLeft.Value != 0 && rightCons.Value != 0 ? 1u : 0u,
                        BinaryOperationType.LogicalOr => constLeft.Value != 0 || rightCons.Value != 0 ? 1u : 0u,
                        
                        BinaryOperationType.Equals => constLeft.Value == rightCons.Value ? 1u : 0u,
                        BinaryOperationType.NotEquals => constLeft.Value != rightCons.Value ? 1u : 0u,
                        BinaryOperationType.UnsignedLessThan => constLeft.Value < rightCons.Value ? 1u : 0u,
                        BinaryOperationType.UnsignedLessThanOrEqual => constLeft.Value <= rightCons.Value ? 1u : 0u,
                        BinaryOperationType.SignedLessThan => (int)constLeft.Value < (int)rightCons.Value ? 1u : 0u,
                        BinaryOperationType.SignedLessThanOrEqual => (int)constLeft.Value <= (int)rightCons.Value ? 1u : 0u,
                        BinaryOperationType.UnsignedGreaterThan => constLeft.Value > rightCons.Value ? 1u : 0u,
                        BinaryOperationType.UnsignedGreaterThanOrEqual => constLeft.Value >= rightCons.Value ? 1u : 0u,
                        BinaryOperationType.SignedGreaterThan => (int)constLeft.Value > (int)rightCons.Value ? 1u : 0u,
                        BinaryOperationType.SignedGreaterThanOrEqual => (int)constLeft.Value >= (int)rightCons.Value ? 1u : 0u,
                        
                        BinaryOperationType.LeftShift => constLeft.Value << (int)rightCons.Value,
                        BinaryOperationType.RightShift => constLeft.Value >> (int)rightCons.Value,
                        _ => throw new InvalidOperationException($"Operation '{bo.Operator}' not implemented for constant folding.")
                    };
                    
                    return result.ToString();
                }
                
                // we do need to do it at runtime
                if (constLeftStr != null) {
                    // left is constant but not evaluatable, we need to put it in the register
                    file.Append(indent, $"mov {reg}, {constLeftStr}  ; Load compile-time constant into register");
                }
                file.Append(leftSetData);
                file.Append(rightSetData);  // scratch reg now has the right value
                
                string arg2 = value ?? scratch 
                    ?? throw new InvalidOperationException("Both value and scratch register were null while trying to resolve binop.");
                
                // so LEFT = reg
                // and RIGHT = arg2 (may be literal)
                
                if (value != null && bo.Operator 
                        is BinaryOperationType.UnsignedDivide 
                        or BinaryOperationType.SignedDivide 
                        or BinaryOperationType.UnsignedModulus 
                        or BinaryOperationType.SignedModulus) {
                    // these instructions require reg, reg args.
                    // so we need to move the constant to a register
                    (scratch, preserve) = AllocateRegister(reg);
                    if (preserve) {
                        file.Push(indent, scratch);
                    }
                    file.Append(indent, $"mov {scratch}, {value}  ; Move constant divisor/modulus to register for division/modulus operation");
                    arg2 = scratch;
                }
                
                if (bo.IsMathematical()) {  // all regular ops
                    string instruction = bo.Operator switch {
                        BinaryOperationType.Add => "add",
                        BinaryOperationType.Subtract => "sub",
                        BinaryOperationType.UnsignedMultiply => "umul",
                        BinaryOperationType.SignedMultiply => "imul",
                        BinaryOperationType.UnsignedDivide => "udiv",
                        BinaryOperationType.SignedDivide => "idiv",
                        BinaryOperationType.UnsignedModulus => "udiv",
                        BinaryOperationType.SignedModulus => "idiv",
                        BinaryOperationType.BitwiseAnd => "and",
                        BinaryOperationType.BitwiseOr => "or",
                        BinaryOperationType.BitwiseXor => "xor",
                        BinaryOperationType.LeftShift => "shl",
                        BinaryOperationType.RightShift => "shr",
                        _ => throw new InvalidOperationException($"Mathematical operation not implemented for '{bo.Operator}'.")
                    };
                    file.Append(indent, $"{instruction} {reg}, {arg2}  ; Perform binary operation");

                    if (bo.Operator is BinaryOperationType.UnsignedModulus or BinaryOperationType.SignedModulus) {
                        // for modulus, the result is in scratch (remainder)
                        file.Append(indent, $"mov {reg}, {arg2}  ; Move modulus result to destination register");
                    }
                }
                else if (bo.IsLogical()) {
                    switch (bo.Operator) {
                        case BinaryOperationType.LogicalAnd: {
                            string labelName = GetUniqueLogicLabel();
                            
                            file.Comment("Perform logical AND operation", indent);
                            file.Append(indent, 
                                $"cmp {reg}, 0",
                                $"mov {reg}, 0  ; assume false for now (we don't need this reg anymore)",
                                $"je {labelName}",
                                $"cmp {arg2}, 0",
                                $"je {labelName}",
                                $"mov {reg}, 1  ; both args are true");
                            file.Label(labelName);  // short circuit label
                            break;
                        }

                        case BinaryOperationType.LogicalOr: {
                            string labelName = GetUniqueLogicLabel();
                            
                            file.Comment("Perform logical OR operation", indent);
                            file.Append(indent, 
                                $"cmp {reg}, 0",
                                $"mov {reg}, 1  ; assume true for now (we don't need this reg anymore)",
                                $"jne {labelName}",
                                $"cmp {arg2}, 0",
                                $"jne {labelName}",
                                $"mov {reg}, 0  ; neither args are true");
                            file.Label(labelName);  // short circuit label
                            break;
                        }
                        
                        default:
                            throw new InvalidOperationException($"Logical operation not implemented for '{bo.Operator}'.");
                    }
                }
                else {  // comparisons
                    string jumpInstruction = bo.Operator switch {
                        BinaryOperationType.Equals => "je",
                        BinaryOperationType.NotEquals => "jne",
                        BinaryOperationType.UnsignedLessThan => "jul",
                        BinaryOperationType.UnsignedLessThanOrEqual => "jule",
                        BinaryOperationType.SignedLessThan => "jil",
                        BinaryOperationType.SignedLessThanOrEqual => "jile",
                        BinaryOperationType.UnsignedGreaterThan => "jug",
                        BinaryOperationType.UnsignedGreaterThanOrEqual => "juge",
                        BinaryOperationType.SignedGreaterThan => "jig",
                        BinaryOperationType.SignedGreaterThanOrEqual => "jige",
                        _ => throw new InvalidOperationException($"Comparison operation not implemented for '{bo.Operator}'.")
                    };
                    
                    string doneOpLabel = GetUniqueLogicLabel();
                    file.Comment($"Perform comparison operation: {Enum.GetName(bo.Operator)}", indent);
                    file.Append(indent, 
                        $"cmp {reg}, {arg2}  ; Compare for binary operation",
                        $"mov {reg}, 1   ; Set destination register to 1 (true)",
                        $"{jumpInstruction} {doneOpLabel}   ; Jump if comparison is true",
                        $"mov {reg}, 0   ; Set destination register to 0 (false)");
                    file.Label(doneOpLabel);
                }

                if (scratch == null) break;
                if (preserve) {
                    file.Pop(indent, scratch);
                }
                else {
                    FreeRegister(scratch);
                }
                break;
            }

            case FunctionCall fc: {
                GenerateFunctionCall(fc, reg, file, indent);
                break;
            }

            case UnaryOperation uo: {
                switch (uo.Operator) {
                    case UnaryOperationType.Negate: {
                        uint? cons = ResolveNumericalExprOrPlaceInReg(uo.Operand, reg, file, indent);
                        if (cons != null) {
                            if (cons == 0) {
                                return "0";  // negating 0 is still 0
                            }
                            uint negated = unchecked((uint)-(int)cons.Value);
                            return negated.ToString();
                        }
                        
                        file.Append(indent, 
                            $"not {reg}  ; Negate value",
                            $"add {reg}, 1  ; Add 1 to complete two's complement negation"
                            );
                        break;
                    }
                    
                    case UnaryOperationType.BitwiseNot: {
                        uint? cons = ResolveNumericalExprOrPlaceInReg(uo.Operand, reg, file, indent);
                        if (cons != null) {
                            uint notted = ~cons.Value;
                            return notted.ToString();
                        }
                        
                        file.Append(indent, $"not {reg}  ; Bitwise NOT operation");
                        break;
                    }

                    case UnaryOperationType.LogicalNot: {
                        string? cons = ResolveExpr(uo.Operand, reg, file, indent);
                        if (cons != null) {
                            return ConstantIsZero(cons) ? "1" : "0";
                        }
                        
                        string doneNotLabel = GetUniqueLogicLabel();
                        file.Append(indent,
                            $"cmp {reg}, 0  ; Compare value to zero for logical NOT",
                            $"je {doneNotLabel}  ; If zero, jump to set to 1",
                            $"mov {reg}, 0  ; Set result to 0 (false)",
                            $"jmp {doneNotLabel}_end");
                        file.Label(doneNotLabel);
                        file.Append(indent, $"mov {reg}, 1  ; Set result to 1 (true)");
                        file.Label(doneNotLabel + "_end");
                        break;
                    }
                    
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                break;
            }
        }

        return null;
    }

    private void GenerateFunctionCall(FunctionCall fc, string returnReg, AssemblyFileBuilder file, bool indent) {
        // No more call validation here - done in analysis phase (partially)
        
        Stack<(string Reg, bool Preserve)> borrowedRegisters = [];

        // the target reg can be the returnReg (to avoid allocating another register)
        // only if it isn't needed for an argument.
        string targetReg = returnReg;
        string[] unavailableRegistersForTarget = CallingConventionArgRegisters.Take(fc.Arguments.Length).ToArray();
        if (unavailableRegistersForTarget.Contains(returnReg)) {
            // we need to borrow another register for the target
            (targetReg, bool preserve) = AllocateRegister(unavailableRegistersForTarget);
            if (preserve) {
                file.Push(indent, targetReg);
            }
            
            borrowedRegisters.Push((targetReg, preserve));
        }
        
        // place pointer to the function in the return register
        string? constTarget = ResolveExpr(fc.Target, targetReg, file, indent);
        string target = constTarget ?? targetReg;
        
        // We need to evaluate all the args and put them in the right registers
        // according to the calling convention
        // r1-r3 for first 3 args, then stack after that
        // functions always return in r0
        // so we need to make sure we borrow r0-r3 if needed
        (string Reg, bool Preserve)? stackTempReg = null;
        
        // first let's try and borrow all the calling convention registers
        // we need to do this regardless of whether they are used as args
        // because they are caller preserved and may be clobbered anyway.
        file.Comment("Borrowing calling convention registers", indent);
        foreach (string reg in CallingConventionArgRegisters.Append(DefaultReturnRegister).Where(r => r != returnReg)) {
            bool preserve = AllocateSpecificRegister(reg);
            borrowedRegisters.Push((reg, preserve));
            if (preserve) file.Push(indent, reg);
        }
        
        // do the stack args first (in reverse order by convention)
        // this is so that we don't have to worry about clobbering the arg
        // registers.
        for (int i = fc.Arguments.Length - 1; i >= CallingConventionArgRegisters.Length; i--) {
            Debug.Assert(i >= CallingConventionArgRegisters.Length);
            
            (string reg, bool preserve) maybeReg = stackTempReg ?? AllocateRegister(returnReg, targetReg);
                
            AssemblyFileBuilder tempFile = new();
            string? constArg = ResolveExpr(fc.Arguments[i], maybeReg.reg, tempFile, indent);

            if (constArg != null) {  // don't bother with temps or anything
                if (!maybeReg.preserve) {
                    FreeRegister(maybeReg.reg);
                }
                
                file.Append(indent, $"" +
                                    $"{GetSizedPushInstruction(4)} " +
                                    $"{constArg}  ; Push argument {i + 1} onto stack");
                file.BlankLine();
                continue;
            }
                
            // okay it used the register
            if (stackTempReg == null) {
                stackTempReg = maybeReg;
                if (maybeReg.preserve) {
                    file.Push(indent, stackTempReg.Value.Reg);
                }
                borrowedRegisters.Push(stackTempReg.Value);
            }
                
            file.Append(tempFile);  // it's in the temp reg now
            file.Append(indent, $"" +
                                $"{GetSizedPushInstruction(4)} " +
                                $"{stackTempReg.Value.Reg}  ; Push argument {i + 1} onto stack");
            file.BlankLine();
        }
        
        // do the register args
        for (int i = 0; i < Math.Min(fc.Arguments.Length, CallingConventionArgRegisters.Length); i++) {
            // register arg
            string argReg = CallingConventionArgRegisters[i];
            
            file.Comment("Prepare argument " + (i + 1), indent);
            PlaceExprInReg(fc.Arguments[i], argReg, file, indent);
            file.BlankLine();
        }
        
        // Now call the function
        file.Append(indent, $"call {target}");
        
        // Clean up stack arguments
        int stackArgsCount = Math.Max(0, fc.Arguments.Length - CallingConventionArgRegisters.Length);
        if (stackArgsCount > 0) {
            int stackCleanupSize = 4 * stackArgsCount;  // assuming 4 bytes per argument (we have no way of knowing size)
            file.Append(indent, $"add {StackPointerRegister}, {stackCleanupSize}  ; Clean up stack arguments");
        }
        
        // Move return value to the desired register if needed
        // and before returning registers because the default return
        // register will be returned.
        if (returnReg != DefaultReturnRegister) {
            file.Append(indent, $"mov {returnReg}, {DefaultReturnRegister}  ; Move return value to desired register");
        }
        
        // Free borrowed registers
        while (borrowedRegisters.Count > 0) {
            (string reg, bool preserve) = borrowedRegisters.Pop();
            if (preserve) {
                file.Pop(indent, reg);
            }
            else {
                FreeRegister(reg);
            }
        }
    }

    private string GetSizedMoveInstruction(CompileTimeValue size) {
        return GetSizedMoveInstruction((int)ResolveCompileConstant(size));
    }
    
    private string GetSizedPushInstruction(CompileTimeValue size) {
        return GetSizedPushInstruction((int)ResolveCompileConstant(size));
    }
    
    private static string GetSizedMoveInstruction(int size) {
        return size switch {
            1 => "mov8",
            2 => "mov16",
            4 => "mov",
            _ => throw new InvalidOperationException($"Sized move instruction not implemented for size {size}.")
        };
    }
    
    private static string GetSizedPushInstruction(int size) {
        return size switch {
            1 => "push8",
            2 => "push16",
            4 => "push",
            _ => throw new InvalidOperationException($"Sized move instruction not implemented for size {size}.")
        };
    }
    
    private static bool ConstantIsZero(string constValue) {
        return uint.TryParse(constValue, out uint val) && val == 0;
    }
    
    private static bool ConstantIsNumerical(string? constValue, out uint? value) {
        if (constValue != null && uint.TryParse(constValue, out uint val)) {
            value = val;
            return true;
        }

        value = null;
        return false;
    }
}
