using CatC.Compiler.Ast;

namespace CatC.Compiler.CodeGen;

public partial class CodeGenerator {
    
    private (string? value, string reg, bool preserve) GenerateExprInScratchRegOrGetConst(
        IValueExpression expr, AssemblyFileBuilder file, bool indent) {
        
        (string scratch, bool preserve) = AllocateRegister();
        
        AssemblyFileBuilder tempFile = new();
        string? constValue = ResolveExpr(expr, scratch, tempFile, indent);
        if (constValue != null) {
            if (!preserve) FreeRegister(scratch);  // we didn't need the scratch reg after all
            return (constValue, null!, false);
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

                if (_globals.Any(g => g.Name == vt.Name)) {
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
                PlaceExprInReg(bo.Left, reg, file, indent);  // we actually need this
                (string? value, string scratch, bool preserve) = GenerateExprInScratchRegOrGetConst(bo.Right, file, indent);
                string arg2 = value ?? scratch;
                
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
                        // BinaryOperationType.LeftShift => "shl",  THIS DOESNT EXIST YET
                        // BinaryOperationType.RightShift => "shr", THIS DOESNT EXIST YET
                        _ => throw new InvalidOperationException($"Mathematical operation not implemented for '{bo.Operator}'.")
                    };
                    file.Append(indent, $"{instruction} {reg}, {arg2}  ; Perform binary operation");

                    if (bo.Operator is BinaryOperationType.UnsignedModulus or BinaryOperationType.SignedModulus) {
                        // for modulus, the result is in scratch (remainder)
                        file.Append(indent, $"mov {reg}, {arg2}  ; Move modulus result to destination register");
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
                    file.Append(indent, 
                        $"mov {reg}, 1  ; Set destination register for comparison result (we'll clear it if false)",
                        $"cmp {reg}, {arg2}  ; Compare for binary operation",
                        $"{jumpInstruction} {doneOpLabel}   ; Jump if comparison is true",
                        $"mov {reg}, 0   ; Set destination register to 0 (false)");
                    file.Label(doneOpLabel);
                }

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
                        PlaceExprInReg(uo.Operand, reg, file, indent);
                        file.Append(indent, 
                            $"not {reg}  ; Negate value",
                            $"add {reg}, 1  ; Add 1 to complete two's complement negation"
                            );
                        break;
                    }
                    
                    case UnaryOperationType.BitwiseNot: {
                        PlaceExprInReg(uo.Operand, reg, file, indent);
                        file.Append(indent, $"not {reg}  ; Bitwise NOT operation");
                        break;
                    }

                    case UnaryOperationType.LogicalNot: {
                        PlaceExprInReg(uo.Operand, reg, file, indent);
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
        
        // place pointer to the function in the return register
        string? constTarget = ResolveExpr(fc.Target, returnReg, file, indent);
        string target = constTarget ?? returnReg;
        
        // We need to evaluate all the args and put them in the right registers
        // according to the calling convention
        // r1-r3 for first 3 args, then stack after that
        // functions always return in r0
        // so we need to make sure we borrow r0-r3 if needed
        Stack<(string Reg, bool Preserve)> borrowedRegisters = [];
        (string Reg, bool Preserve)? stackTempReg = null;
        for (int i = 0; i < fc.Arguments.Length; i++) {
            // stack arg
            if (i >= CallingConventionArgRegisters.Length) {
                (string reg, bool preserve) maybeReg = stackTempReg ?? AllocateRegister(returnReg);
                
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
                    borrowedRegisters.Push(stackTempReg.Value);
                    file.Push(indent, stackTempReg.Value.Reg);
                }
                
                file.Append(tempFile);  // it's in the temp reg now
                file.Append(indent, $"" +
                                    $"{GetSizedPushInstruction(4)} " +
                                    $"{stackTempReg.Value.Reg}  ; Push argument {i + 1} onto stack");
                file.BlankLine();
                continue;
            }

            // register arg
            string argReg = CallingConventionArgRegisters[i];
            
            bool preserve = AllocateSpecificRegister(argReg);
            borrowedRegisters.Push((argReg, preserve));

            file.Comment("Prepare argument " + (i + 1), indent);
            if (preserve) file.Push(indent, argReg);
            PlaceExprInReg(fc.Arguments[i], argReg, file, indent);
            file.BlankLine();
        }
        
        // Now call the function
        file.Append(indent, $"call {target}");
        
        // Clean up stack arguments
        int stackArgsCount = Math.Max(0, fc.Arguments.Length - CallingConventionArgRegisters.Length);
        if (stackArgsCount > 0) {
            int stackCleanupSize = 4 * stackArgsCount;  // assuming 4 bytes per argument (we have no way of knowing size)
            file.Append(indent, $"add {BasePointerRegister}, {stackCleanupSize}  ; Clean up stack arguments");
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
        
        // Move return value to the desired register if needed
        if (returnReg != DefaultReturnRegister) {
            file.Append(indent, $"mov {returnReg}, {DefaultReturnRegister}  ; Move return value to desired register");
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
}
