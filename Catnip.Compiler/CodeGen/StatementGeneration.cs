using System.Diagnostics;
using Catnip.Compiler.Analysis;
using Catnip.Compiler.Ast;

namespace Catnip.Compiler.CodeGen;

public partial class CodeGenerator {
    
    private void GenerateStatement(Statement statement, AssemblyFileBuilder file, bool indent = true, bool isLastInFunc = false) {
        // Record where in the Catnip source this code came from, so the assembler can carry it
        // into the debug table and a debugger can step the .nip file rather than the generated
        // assembly. Statements without location info (compiler-synthesised ones) leave the
        // previous mapping in place, which is the closest true answer available.
        file.SourceLocation(statement.FileInformation, indent);

        switch (statement) {
            case StatementBlock block: {
                file.Comment("Begin Statement Block", indent);
                for (int i = 0; i < block.Statements.Length; i++) {
                    Statement stmt = block.Statements[i];
                    GenerateStatement(stmt, file, indent, isLastInFunc && i == block.Statements.Length - 1);
                }
                file.Comment("End Statement Block", indent);
                break;
            }
            
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
                (string? value, string? scratch, bool preserve) = GenerateExprInScratchRegOrGetConst(ifStatement.Condition, file, indent);

                if (value != null) {
                    file.Comment("Constant condition has been folded", indent);
                    // constant condition
                    // time to do some constant folding
                    if (ConstantIsZero(value)) {  // condition is false
                        // generate else statements
                        GenerateStatement(ifStatement.ElseStatements, file, indent);
                    }
                    else {
                        // generate then statements
                        GenerateStatement(ifStatement.ThenStatements, file, indent);
                    }
                    
                    file.Comment("End If Statement", indent);
                    break;
                }
                Debug.Assert(scratch != null);
                
                string logicLabel = GetUniqueLogicLabel();
                
                file.Append(indent, 
                    $"cmp {scratch}, 0",
                    $"je {logicLabel}_else  ; if condition is false, jump to else");
                
                // then
                GenerateStatement(ifStatement.ThenStatements, file, indent);
                file.Append(indent, $"jmp {logicLabel}_end  ; jump to end after then");
                
                // else
                file.Label($"{logicLabel}_else");
                GenerateStatement(ifStatement.ElseStatements, file, indent);
                
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
                
                file.Comment("While Loop", indent);
                file.Label(loopLabel + "_start");
                
                (string? value, string? scratch, bool preserve) = GenerateExprInScratchRegOrGetConst(whileStatement.Condition, file, indent);

                if (value != null) {
                    // constant condition
                    // time to do some constant folding
                    if (ConstantIsZero(value)) {  // condition is false
                        // do nothing, loop will never run
                        break;
                    }

                    // generate body statements
                    GenerateStatement(whileStatement.BodyStatements, file, indent);
                    file.Append(indent, $"jmp {loopLabel}_start  ; jump back to start of loop");
                    break;
                }
                Debug.Assert(scratch != null);
                
                file.Append(indent, 
                    $"cmp {scratch}, 0",
                    $"je {loopLabel}_end  ; if condition is false, exit loop");
                
                GenerateStatement(whileStatement.BodyStatements, file);
                
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
                    bool preserve = AllocateSpecificRegister(DefaultReturnRegister);
                    
                    // doesn't matter if it's already in use since we're about
                    // to return. But it can be in use by things like if/while etc.
                    // But we need to ensure it's reserved so it doesn't get allocated
                    // while evaluating the return value.
                    
                    PlaceExprInReg(returnStatement.Value, DefaultReturnRegister, file, indent);
                    if (!preserve) FreeRegister(DefaultReturnRegister);
                }

                if (!isLastInFunc) file.Append(indent, "jmp .end");
                break;
            }

            case FunctionCall functionCall: {
                bool preserve = AllocateSpecificRegister(DefaultReturnRegister);
                if (preserve) {
                    file.Push(indent, DefaultReturnRegister, "preserve needed temp register");
                }
                GenerateFunctionCall(functionCall, DefaultReturnRegister, file, indent);
                if (!preserve) {
                    FreeRegister(DefaultReturnRegister);
                }
                else {
                    file.Pop(indent, DefaultReturnRegister, "free function call temp return register");
                }
                break;
            }

            case SwitchStatement switchStatement: {
                const int ifElseChainThreshold = 2;
                
                // if else
                // direct array
                // mod + array
                // salt hash + mod + array (rotate salt to get good hashes)
                
                // once find value to avoid hash collision, also do if check
                
                // collate all constant case values
                List<uint> vals = [];
                foreach ((IValueExpression[] valueExprs, _) in switchStatement.Cases) {
                    vals.AddRange(valueExprs.Select(expr => ResolveCompileConstant(CompileTimeValue.From(expr))));
                }
                

                if (vals.Count <= ifElseChainThreshold) {
                    List<(IValueExpression cond, Statement statement)> conditions = [];
                    foreach ((IValueExpression[] values, Statement statements) in switchStatement.Cases) {
                        List<BinaryOperation> conds = values
                            .Select(value => new BinaryOperation(switchStatement.Expression, BinaryOperationType.Equals, value))
                            .ToList();
                        
                        IValueExpression? combined = conds
                            .Aggregate((IValueExpression?)null, (acc, cond) => 
                                acc == null
                                    ? cond 
                                    : new BinaryOperation(acc, BinaryOperationType.LogicalOr, cond));
                        
                        Debug.Assert(combined != null);
                        
                        conditions.Add((combined, statements));
                    }
                    
                    Statement ifElseChain = conditions
                        .Reverse<(IValueExpression cond, Statement statement)>()
                        .Aggregate(switchStatement.DefaultStatements, (elseAcc, current) => 
                            new IfStatement(current.cond, current.statement, elseAcc));
                    
                    GenerateStatement(ifElseChain, file, indent);
                    break;
                }
                
                // find a valid and operand to give unique values for each val
                int andOperand = 1;
                while (true) {
                    int operand = andOperand;
                    if (AreValuesUnique(vals.Select(v => v & (uint)operand))) {
                        break;
                    }

                    andOperand = andOperand * 2 + 1;
                }
                
                // okay we have a very fast hash function
                int uniqueValues = CountUniqueValuesForAnd(andOperand);
                
                // let's generate the table lookup
                AssemblyFileBuilder table = new();
                string tableLabel = GetSwitchTableLabel(table);
                string endLabel = GetUniqueLogicLabel();

                file.Comment("Switch jump table lookup (AND hashing)", indent);
                (string scratch, bool preserve) = AllocateRegister();
                (string exprReg, bool preserveExprReg) = AllocateRegister();
                if (preserve) file.Push(indent, scratch);
                if (preserveExprReg) file.Push(indent, exprReg);
                
                PlaceExprInReg(switchStatement.Expression, scratch, file, indent);
                // 18 cycles
                file.Append(indent, $"mov {exprReg}, {scratch}  ; save so we can compare later");
                file.Append(indent, $"and {scratch}, {andOperand}  ; hash the switch expression");
                file.Append(indent, $"shl {scratch}, 2   ; multiply by 4 to get byte offset for 32bit addr");
                file.Append(indent, $"add {scratch}, {tableLabel}  ; add base address of jump table");
                file.Append(indent, $"mov {scratch}, [{scratch}]  ; get jump address from table");
                file.Append(indent, $"jmp {scratch}  ; jump to case");
                file.BlankLine();
                
                // uniqueValues is how many entries will be in our table
                string[] jumpLabels = new string[uniqueValues];
                
                // by default anything that isn't set will jump to default
                string defaultBranch = GetGlobalUniqueUnscopedLogicLabel();
                for (int i = 0; i < uniqueValues; i++) {
                    jumpLabels[i] = defaultBranch;
                }
                
                Dictionary<string, (Statement, IValueExpression[])> caseStatements = [];
                foreach ((IValueExpression[] valueExprs, Statement code) in switchStatement.Cases) {
                    string label = GetGlobalUniqueUnscopedLogicLabel();
                    caseStatements.Add(label, (code, valueExprs));
                    
                    foreach (IValueExpression valExpr in valueExprs) {
                        uint val = ResolveCompileConstant(CompileTimeValue.From(valExpr));
                        int index = (int)(val & (uint)andOperand);
                        Debug.Assert(jumpLabels[index] == defaultBranch);
                        jumpLabels[index] = label;
                    }
                }

                // generate the branch code segments
                foreach ((string label, (Statement code, IValueExpression[] exprs)) in caseStatements) {
                    string matchesCondLabel = GetUniqueLogicLabel();
                    file.Label(label);
                    foreach (IValueExpression expr in exprs) {
                        uint realVal = ResolveCompileConstant(CompileTimeValue.From(expr));
                        file.Append(indent, $"cmp {exprReg}, {realVal}   ; make sure it wasn't just a hash collision");
                        file.Append(indent, $"je {matchesCondLabel}");
                    }
                    file.Append(indent, $"jmp {defaultBranch}   ; it was a hash collision, go to default");
                    
                    file.Label(matchesCondLabel);
                    GenerateStatement(code, file, indent);

                    file.Append(indent, $"jmp {endLabel}");
                    file.BlankLine();
                }
                
                // write the default case
                file.Label(defaultBranch);
                GenerateStatement(switchStatement.DefaultStatements, file, indent);
                file.Append(indent, $"jmp {endLabel}");
                file.BlankLine();
                
                // now for the table
                for (int i = 0; i < uniqueValues; i++) {
                    table.Append(indent, $"d32 {jumpLabels[i]}");
                }
                
                // free registers
                file.Label(endLabel);
                if (preserve) file.Pop(indent, scratch); else FreeRegister(scratch);
                if (preserveExprReg) file.Pop(indent, exprReg); else FreeRegister(exprReg);
                file.Comment("End switch statement", indent);
                
                break;
            }
        }
    }
    
    private static int CountUniqueValuesForAnd(int operand) {
        int res = 1;
        while (operand != 0) {
            res *= 1 + (operand & 0b1);
            operand >>= 1;
        }
        return res;
    }

    private static bool AreValuesUnique(IEnumerable<uint> values) {
        HashSet<uint> seen = [];
        return values.All(seen.Add);
    }
    
    private void GenerateVariableAssignment(VariableAssignment assignment, AssemblyFileBuilder file, bool indent) {
        file.Comment("Variable Assignment", indent);
        
        (string? value, string? reg, bool preserve) = GenerateExprInScratchRegOrGetConst(assignment.Value, file, indent);
        if (value != null) {
            GenerateVariableAssignment(assignment.Target, value, file, indent);
            return;
        }
        Debug.Assert(reg != null);
        
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

        // if the source value is a register, we can't use it to get the addr
        string[] dontGiveRegisters = Analyser.ValidRegisters.Contains(sourceValue) ? [sourceValue] : [];
        (string? value, string? reg, bool preserve) = GenerateExprInScratchRegOrGetConst(deref.Left, file, 
            indent, dontGiveRegisters);
        string src = value ?? reg ?? throw new Exception("Failed to generate variable assignment: could not get source value.");
        
        int size = (int)ResolveCompileConstant(CompileTimeValue.From(deref.Right));
        file.Append(indent, $"{GetSizedMoveInstruction(size)} [{src}], {sourceValue}  ; store to variable");

        if (value == null) {
            Debug.Assert(reg != null);
            if (preserve) {
                file.Pop(indent, reg);
            }
            else {
                FreeRegister(reg);
            }
        }
    }
}
