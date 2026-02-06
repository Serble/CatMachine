using Catnip.Compiler.Ast;

namespace Catnip.Compiler.Analysis;

public class Analyser(ParsedElement[] elements, BinaryGlobal[] binaryGlobals) {
    public static readonly string[] ValidRegisters = [
        "r0", "r1", "r2", "r3", "r4", "r5", "r6", "r7",
        "fl", "sp", "ip", "it"  // probably don't use these
    ];
    
    private readonly Stack<int> _scopes = [];  // each scope is length of locals at scope start
    
    private readonly List<Struct> _structs = [];
    private readonly List<Function> _functions = [];

    private readonly List<string> _locals = [];
    private readonly List<string> _globals = [];
    private readonly List<(ParsedElement Element, string Struct, string? Member)> _neededStructs = [];
    private readonly List<(ParsedElement Element, string FunctionName, IValueExpression[] Args)> _functionCalls = [];
    private readonly List<(ParsedElement Element, CompileTimeValue Size)> _mustBeMovCompatible = [];
    private readonly List<(ParsedElement Element, string GlobalName)> _neededGlobals = [];
    
    private readonly List<CompilationFailureException> _errors = [];

    private void BeginScope() {
        _scopes.Push(_locals.Count);
    }

    private void EndScope() {
        if (_scopes.Count == 0) {
            throw new Exception("No scope to end.");
        }

        int scopeStart = _scopes.Pop();
        while (_locals.Count > scopeStart) {
            _locals.RemoveAt(_locals.Count - 1);
        }

        if (scopeStart != _locals.Count) {
            throw new Exception("Scope end did not restore locals to correct state. Expected " + 
                                scopeStart + " but got " + _locals.Count);
        }
    }
    
    private void ValidateRegister(ParsedElement element, string register) {
        if (!ValidRegisters.Contains(register.ToLower())) {
            _errors.Add(new CompilationFailureException(element, $"Invalid register '{register.ToLower()}'."));
        }
    }

    public CatProgram Analyse() {
        List<Statement> topLevelStatements = [];
        
        // these are all implicitly global, so add them to the global list right away
        _globals.AddRange(binaryGlobals.Select(b => b.Name));

        // top level
        foreach (ParsedElement element in elements) {
            switch (element) {
                case Statement ps: {
                    if (ps is LocalDeclaration) {
                        _errors.Add(new CompilationFailureException(element, "Local declarations are not allowed at the top level."));
                    }
                    
                    topLevelStatements.Add(ps);
                    break;
                }
                
                case Struct pse: {
                    _structs.Add(pse);
                    break;
                }

                case Function pfe: {
                    Function func = AnalyseFunction(pfe);
                    if (_functions.Any(f => f.Name == func.Name)) {
                        _errors.Add(new CompilationFailureException(element, $"Function '{func.Name}' is already defined."));
                    }
                    _functions.Add(func);
                    _globals.Add(func.Name);
                    _locals.Clear();
                    break;
                }

                default: {
                    throw new Exception("Unknown parsed element type.");
                }
            }
        }

        foreach (Statement statement in topLevelStatements) {
            AnalyseStatement(statement);
        }

        foreach ((ParsedElement element, string functionName, IValueExpression[] args) in _functionCalls) {
            Function? function = _functions.FirstOrDefault(f => f.Name == functionName);
            if (function == null) {
                _errors.Add(new CompilationFailureException(element, $"Function '{functionName}' is not defined."));
                continue;
            }

            if (function.Parameters.Length != args.Length) {
                _errors.Add(new CompilationFailureException(element, 
                    $"Function '{functionName}' expects {function.Parameters.Length} arguments, but {args.Length} were provided."));
            }
        }

        foreach ((ParsedElement element, string str, string? mem) in _neededStructs) {
            Struct? structure = _structs.FirstOrDefault(s => s.Name == str);
            if (structure == null) {
                _errors.Add(new CompilationFailureException(element, $"Struct '{str}' is not defined."));
                continue;
            }

            if (mem == null) continue;
            if (structure.Fields.All(f => f.Name != mem)) {
                _errors.Add(new CompilationFailureException(element, $"Struct '{str}' does not have a member named '{mem}'."));
            }
        }
        
        Struct[] structsArray = _structs.ToArray();
        foreach ((ParsedElement element, CompileTimeValue size) in _mustBeMovCompatible) {
            uint resolvedSize = size.Resolve(structsArray);
            if (resolvedSize is not (1 or 2 or 4)) {
                _errors.Add(new CompilationFailureException(element, 
                    $"Size '{resolvedSize}' is not mov-compatible. Only sizes 1, 2 and 4 are allowed."));
            }
        }

        foreach ((ParsedElement element, string neededGlobal) in _neededGlobals) {
            if (!_globals.Contains(neededGlobal)) {
                _errors.Add(new CompilationFailureException(element, $"Global variable '{neededGlobal}' is not defined."));
            }
        }
        
        if (_errors.Count > 0) {
            throw new AggregateException(_errors);
        }

        return new CatProgram(structsArray, topLevelStatements.ToArray(), _functions.ToArray(), binaryGlobals);
    }

    private Function AnalyseFunction(Function function) {
        foreach (VarNameSize arg in function.Parameters) {
            _locals.Add(arg.Name);
        }
        
        foreach (Statement statement in function.Statements) {
            AnalyseStatement(statement);
        }
        
        return function;
    }

    private void AnalyseStatement(Statement statement) {
        switch (statement) {
            case LocalDeclaration ld: {
                if (_locals.Contains(ld.Name)) {
                    _errors.Add(new CompilationFailureException(statement, 
                        $"Local variable '{ld.Name}' is already declared in this scope."));
                }

                if (ld.Initial != null) {
                    AnalyseExpression(statement, ld.Initial);
                    _mustBeMovCompatible.Add((statement, ld.Size));
                }
                
                _locals.Add(ld.Name);
                break;
            }

            case GlobalDeclaration gd: {
                if (_locals.Contains(gd.Name)) {
                    _errors.Add(new CompilationFailureException(statement, 
                        $"Variable '{gd.Name}' is already declared in this scope."));
                }
                
                if (gd.Initial != null) {
                    AnalyseExpression(statement, gd.Initial);
                    _mustBeMovCompatible.Add((statement, gd.Size));
                }
                
                _globals.Add(gd.Name);
                break;
            }

            case VariableAssignment ass: {
                AnalyseExpression(statement, ass.Value);

                if (ass.Target is not BinaryOperation { Operator: BinaryOperationType.Dereference } bo) {
                    _errors.Add(new CompilationFailureException(statement, 
                        "Variable assignment target must be a dereference."));
                    break;
                }

                AnalyseExpression(ass, bo);
                
                _mustBeMovCompatible.Add((statement, CompileTimeValue.From(bo.Right)));
                break;
            }

            case IfStatement ifs: {
                AnalyseExpression(statement, ifs.Condition);
                BeginScope();
                foreach (Statement thenStmnt in ifs.ThenStatements) {
                    AnalyseStatement(thenStmnt);
                }
                EndScope();
                break;
            }

            case WhileStatement ws: {
                AnalyseExpression(statement, ws.Condition);
                BeginScope();
                foreach (Statement bodyStatement in ws.BodyStatements) {
                    AnalyseStatement(bodyStatement);
                }
                EndScope();
                break;
            }

            case InlineAsm ilasm: {
                foreach ((string Register, IValueExpression Value) inp in ilasm.Inputs) {
                    AnalyseExpression(statement, inp.Value);
                    ValidateRegister(statement, inp.Register);
                }
                foreach ((string Register, IValueExpression Value) outp in ilasm.Outputs) {
                    ValidateRegister(statement, outp.Register);
                    AnalyseExpression(statement, outp.Value);

                    if (outp.Value is not BinaryOperation {
                            Operator: BinaryOperationType.Dereference
                        } && CompileTimeValue.IsValid(outp.Value)) {
                        _errors.Add(new CompilationFailureException(statement, 
                            "Inline assembly output must be a variable reference (var:size)."));
                    }
                }
                foreach (string clobber in ilasm.Clobbers) {
                    ValidateRegister(statement, clobber);
                }
                break;
            }

            case ReturnStatement retStmt: {
                if (retStmt.Value != null) {
                    AnalyseExpression(statement, retStmt.Value);
                }
                break;
            }
            
            case FunctionCall fc: {
                AnalyseExpression(statement, fc);
                break;
            }

            default: {
                throw new Exception($"Statement analysis not implemented yet for '{statement.GetType().Name}'.");
            }
        }
    }

    private void AnalyseExpression(ParsedElement element, IValueExpression expr) {
        while (true) {
            switch (expr) {
                // these are fine as-is
                case IntegerLiteral:
                case StringLiteral:
                    break;
                
                case StructSizeof sso: {
                    _neededStructs.Add((element, sso.StructName, null));
                    break;
                }
                
                case StructOffsetOf sof: {
                    _neededStructs.Add((element, sof.StructName, sof.ParamName));
                    break;
                }
                
                case VariableToken vt: {
                    if (_locals.Contains(vt.Name)) {
                        break;  // local variable, it exists
                    }
                    
                    _neededGlobals.Add((element, vt.Name));  // global variable, will validate later
                    break;
                }

                case BinaryOperation bo: {
                    AnalyseExpression(element, bo.Left);
                    expr = bo.Right;

                    switch (bo.Operator) {
                        case BinaryOperationType.Dereference: {
                            if (!CompileTimeValue.IsValid(bo.Right)) {
                                _errors.Add(new CompilationFailureException(element, 
                                    "Dereference size must be a compile-time constant."));
                                break;
                            }
                            
                            // dereferences must be mov-compatible
                            _mustBeMovCompatible.Add((element, CompileTimeValue.From(bo.Right)));
                            break;
                        }
                    }
                    continue;
                }

                case UnaryOperation uo: {
                    expr = uo.Operand;
                    continue;
                }

                case FunctionCall fc: {
                    foreach (IValueExpression arg in fc.Arguments) {
                        AnalyseExpression(element, arg);
                    }
                    
                    if (fc.Target is VariableToken vt) {
                        if (_locals.Contains(vt.Name)) {
                            break;  // calling a local variable, can't validate further
                        }
                        _functionCalls.Add((element, vt.Name, fc.Arguments));
                    }
                    break;
                }
            }

            break;
        }
    }
}
