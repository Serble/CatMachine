using Catnip.Compiler.Ast;

namespace Catnip.Compiler.CodeGen;

public partial class CodeGenerator {
    
    private void GenerateStatement(Statement statement, AssemblyFileBuilder file, bool indent = true) {
        switch (statement) {
            case LocalDeclaration localDeclaration: {
                _currentStackOffset += (int)ResolveCompileConstant(localDeclaration.Size);
                _localVarOffsets[localDeclaration.Name] = _currentStackOffset;

                if (localDeclaration.Initial != null) {
                    BinaryOperation deref = new(
                        new VariableToken(localDeclaration.Name),
                        BinaryOperationType.Dereference,
                        localDeclaration.Size);
                    GenerateVariableAssignment(new VariableAssignment(deref, localDeclaration.Initial), file, indent);
                }
                break;
            }

            case VariableAssignment variableAssignment: {
                GenerateVariableAssignment(variableAssignment, file, indent);
                break;
            }

            case GlobalDeclaration globalDeclaration: {
                _globals.Add((globalDeclaration.Name, (int)ResolveCompileConstant(globalDeclaration.Size)));
                
                if (globalDeclaration.Initial != null) {
                    BinaryOperation deref = new(
                        new VariableToken(globalDeclaration.Name),
                        BinaryOperationType.Dereference,
                        globalDeclaration.Size);
                    GenerateVariableAssignment(new VariableAssignment(deref, globalDeclaration.Initial), file, indent);
                }
                break;
            }

            case IfStatement ifStatement: {
                file.Comment("If Statement", indent);
                (string? value, string scratch, bool preserve) = GenerateExprInScratchRegOrGetConst(ifStatement.Condition, file, indent);

                if (value != null) {
                    file.Comment("Constant condition has been folded", indent);
                    // constant condition
                    // time to do some constant folding
                    if (ConstantIsZero(value)) {  // condition is false
                        // generate else statements
                        foreach (Statement elseStmnt in ifStatement.ElseStatements) {
                            GenerateStatement(elseStmnt, file, indent);
                        }
                    }
                    else {
                        // generate then statements
                        foreach (Statement thenStmnt in ifStatement.ThenStatements) {
                            GenerateStatement(thenStmnt, file, indent);
                        }
                    }
                    
                    file.Comment("End If Statement", indent);
                    break;
                }
                
                string logicLabel = GetUniqueLogicLabel();
                
                file.Append(indent, 
                    $"cmp {scratch}, 0",
                    $"je {logicLabel}_else  ; if condition is false, jump to else");
                
                // then
                foreach (Statement thenStmnt in ifStatement.ThenStatements) {
                    GenerateStatement(thenStmnt, file, indent);
                }
                file.Append(indent, $"jmp {logicLabel}_end  ; jump to end after then");
                
                // else
                file.Label($"{logicLabel}_else");
                foreach (Statement elseStmnt in ifStatement.ElseStatements) {
                    GenerateStatement(elseStmnt, file, indent);
                }
                
                // end
                file.Label($"{logicLabel}_end");
                
                if (preserve) {
                    file.Pop(indent, scratch);
                }
                else {
                    FreeRegister(scratch);
                }
                file.Comment("End If Statement", indent);
                break;
            }

            case WhileStatement whileStatement: {
                string loopLabel = GetUniqueLogicLabel();
                (string? value, string scratch, bool preserve) = GenerateExprInScratchRegOrGetConst(whileStatement.Condition, file, indent);

                if (value != null) {
                    // constant condition
                    // time to do some constant folding
                    if (ConstantIsZero(value)) {  // condition is false
                        // do nothing, loop will never run
                        break;
                    }

                    // generate body statements
                    file.Label(loopLabel + "_start");
                    foreach (Statement thenStmnt in whileStatement.BodyStatements) {
                        GenerateStatement(thenStmnt, file, indent);
                    }
                    file.Append(indent, $"jmp {loopLabel}_start  ; jump back to start of loop");
                    break;
                }
                
                file.Label(loopLabel + "_start");
                file.Append(indent, 
                    $"cmp {scratch}, 0",
                    $"je {loopLabel}_end  ; if condition is false, exit loop");
                
                foreach (Statement bodyStatement in whileStatement.BodyStatements) {
                    GenerateStatement(bodyStatement, file);
                }
                
                file.Append(indent, $"jmp {loopLabel}_start  ; jump back to start of loop");
                file.Label(loopLabel + "_end");
                
                if (preserve) {
                    file.Pop(indent, scratch);
                }
                else {
                    FreeRegister(scratch);
                }
                break;
            }

            case InlineAsm inlineAsm: {
                file.Comment("Inline Assembly Block", indent);
                HashSet<string> toBorrow = [];
                foreach ((string register, IValueExpression _) in inlineAsm.Inputs) {
                    toBorrow.Add(register);
                }
                foreach ((string register, IValueExpression _) in inlineAsm.Outputs) {
                    toBorrow.Add(register);
                }
                foreach (string clobber in inlineAsm.Clobbers) {
                    toBorrow.Add(clobber);
                }
                
                Stack<(string Register, bool Preserved)> borrowed = [];
                foreach (string reg in toBorrow) {
                    bool preserve = AllocateSpecificRegister(reg);
                    borrowed.Push((reg, preserve));
                    if (preserve) {
                        file.Push(indent, reg);
                    }
                }
                
                // Place inputs into their registers
                foreach ((string register, IValueExpression value) in inlineAsm.Inputs) {
                    file.Comment($"Prepare inline asm input {register}", indent);
                    PlaceExprInReg(value, register, file, indent);
                }
                
                // Emit the assembly
                file.Comment("Begin Inline Assembly", indent);
                file.BlankLine();
                file.Append(indent, inlineAsm.Asm);
                file.BlankLine();
                file.Comment("End Inline Assembly", indent);
                
                // Retrieve outputs from their registers
                foreach ((string register, IValueExpression var) in inlineAsm.Outputs) {
                    // Store the output register value into the variable
                    GenerateVariableAssignment(var, register, file, indent);
                }
                
                // Restore borrowed registers
                while (borrowed.Count > 0) {
                    (string reg, bool preserve) = borrowed.Pop();
                    if (preserve) {
                        file.Pop(indent, reg);
                    }
                    else {
                        FreeRegister(reg);
                    }
                }

                break;
            }

            case ReturnStatement returnStatement: {
                if (returnStatement.Value != null) {
                    PlaceExprInReg(returnStatement.Value, DefaultReturnRegister, file, indent);
                }

                file.Append(indent, "jmp .end");
                break;
            }

            case FunctionCall functionCall: {
                GenerateFunctionCall(functionCall, DefaultReturnRegister, file, indent);
                break;
            }
        }
    }
    
    private void GenerateVariableAssignment(VariableAssignment assignment, AssemblyFileBuilder file, bool indent) {
        file.Comment("Variable Assignment", indent);
        
        (string? value, string reg, bool preserve) = GenerateExprInScratchRegOrGetConst(assignment.Value, file, indent);
        if (value != null) {
            GenerateVariableAssignment(assignment.Target, value, file, indent);
            return;
        }
        
        GenerateVariableAssignment(assignment.Target, reg, file, indent);
        if (value == null) {
            if (preserve) {
                file.Pop(indent, reg);
            }
            else {
                FreeRegister(reg);
            }
        }
    }

    private void GenerateVariableAssignment(IValueExpression target, string sourceValue, AssemblyFileBuilder file, bool indent) {
        BinaryOperation deref = (BinaryOperation)target;
        if (deref.Operator != BinaryOperationType.Dereference) {
            throw new Exception("Variable assignment target must be a dereference operation.");
        }

        (string? value, string reg, bool preserve) = GenerateExprInScratchRegOrGetConst(deref.Left, file, indent);
        string src = value ?? reg;
        
        int size = (int)ResolveCompileConstant(CompileTimeValue.From(deref.Right));
        file.Append(indent, $"{GetSizedMoveInstruction(size)} [{src}], {sourceValue}  ; store to variable");

        if (value == null) {
            if (preserve) {
                file.Pop(indent, reg);
            }
            else {
                FreeRegister(reg);
            }
        }
    }
}
