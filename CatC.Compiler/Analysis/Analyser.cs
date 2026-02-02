using CatC.Compiler.Ast;
using CatC.Compiler.Parser;

namespace CatC.Compiler.Analysis;

public class Analyser(ParsedElement[] elements) {
    private static readonly string[] ValidRegisters = [
        "r0", "r1", "r2", "r3", "r4", "r5", "r6", "r7",
        "fl", "sp", "ip", "it"  // probably don't use these
    ];
    
    private readonly Queue<int> _scopes = [];  // each scope is length of locals at scope start
    
    private readonly List<Struct> _structs = [];
    private readonly List<Function> _functions = [];

    private readonly List<string> _locals = [];
    private readonly List<(string Struct, string? Member)> _neededStructs = [];
    private readonly List<(string FunctionName, IValueExpression[] Args)> _functionCalls = [];

    private void BeginScope() {
        _scopes.Enqueue(_locals.Count);
    }

    private void EndScope() {
        if (_scopes.Count == 0) {
            throw new Exception("No scope to end.");
        }

        int scopeStart = _scopes.Dequeue();
        while (_locals.Count > scopeStart) {
            _locals.RemoveAt(_locals.Count - 1);
        }
    }
    
    private static void ValidateRegister(string register) {
        if (!ValidRegisters.Contains(register.ToLower())) {
            throw new CompilationFailureException($"Invalid register '{register.ToLower()}'.");
        }
    }

    public CatProgram Analyse() {
        List<IStatement> topLevelStatements = [];

        // top level
        foreach (ParsedElement element in elements) {
            switch (element) {
                case ParsedStatementElement ps: {
                    if (ps.Statement is LocalDeclaration) {
                        throw new CompilationFailureException("Local declarations are not allowed at the top level.");
                    }
                    
                    topLevelStatements.Add(ps.Statement);
                    break;
                }
                
                case ParsedStructElement pse: {
                    _structs.Add(pse.Struct);
                    break;
                }

                case ParsedFunctionElement pfe: {
                    Function func = AnalyseFunction(pfe.Function);
                    if (_functions.Any(f => f.Name == func.Name)) {
                        throw new CompilationFailureException($"Function '{func.Name}' is already defined.");
                    }
                    _functions.Add(func);
                    _locals.Clear();
                    break;
                }

                default: {
                    throw new Exception("Unknown parsed element type.");
                }
            }
        }

        foreach (IStatement statement in topLevelStatements) {
            AnalyseStatement(statement);
        }

        foreach ((string functionName, IValueExpression[] args) in _functionCalls) {
            Function? function = _functions.FirstOrDefault(f => f.Name == functionName);
            if (function == null) {
                throw new CompilationFailureException($"Function '{functionName}' is not defined.");
            }

            if (function.Parameters.Length != args.Length) {
                throw new CompilationFailureException(
                    $"Function '{functionName}' expects {function.Parameters.Length} arguments, but {args.Length} were provided.");
            }
        }

        foreach ((string str, string? mem) in _neededStructs) {
            Struct? structure = _structs.FirstOrDefault(s => s.Name == str);
            if (structure == null) {
                throw new CompilationFailureException($"Struct '{str}' is not defined.");
            }

            if (mem == null) continue;
            if (structure.Fields.All(f => f.Name != mem)) {
                throw new CompilationFailureException($"Struct '{str}' does not have a member named '{mem}'.");
            }
        }

        return new CatProgram(_structs.ToArray(), topLevelStatements.ToArray(), _functions.ToArray());
    }

    private Function AnalyseFunction(Function function) {
        foreach (VarNameSize arg in function.Parameters) {
            _locals.Add(arg.Name);
        }
        
        foreach (IStatement statement in function.Statements) {
            AnalyseStatement(statement);
        }
        
        return function;
    }

    private void AnalyseStatement(IStatement statement) {
        switch (statement) {
            case LocalDeclaration ld: {
                if (_locals.Contains(ld.Name)) {
                    throw new CompilationFailureException($"Local variable '{ld.Name}' is already declared in this scope.");
                }

                if (ld.Initial != null) {
                    AnalyseExpression(ld.Initial);
                }
                    
                _locals.Add(ld.Name);
                break;
            }

            case GlobalDeclaration gd: {
                if (_locals.Contains(gd.Name)) {
                    throw new CompilationFailureException($"Variable '{gd.Name}' is already declared in this scope.");
                }
                
                if (gd.Initial != null) {
                    AnalyseExpression(gd.Initial);
                }
                    
                _locals.Add(gd.Name);
                break;
            }

            case VariableAssignment ass: {
                AnalyseExpression(ass.Value);

                if (ass.Target is not BinaryOperation { Operator: BinaryOperationType.Dereference }) {
                    throw new CompilationFailureException("Variable assignment target must be a dereference");
                }
                break;
            }

            case IfStatement ifs: {
                AnalyseExpression(ifs.Condition);
                BeginScope();
                foreach (IStatement thenStmnt in ifs.ThenStatements) {
                    AnalyseStatement(thenStmnt);
                }
                EndScope();
                break;
            }

            case WhileStatement ws: {
                AnalyseExpression(ws.Condition);
                BeginScope();
                foreach (IStatement bodyStatement in ws.BodyStatements) {
                    AnalyseStatement(bodyStatement);
                }
                EndScope();
                break;
            }

            case InlineAsm ilasm: {
                foreach ((string Register, IValueExpression Value) inp in ilasm.Inputs) {
                    AnalyseExpression(inp.Value);
                    ValidateRegister(inp.Register);
                }
                foreach ((string Register, IValueExpression Value) outp in ilasm.Outputs) {
                    ValidateRegister(outp.Register);
                    AnalyseExpression(outp.Value);

                    if (outp.Value is not BinaryOperation {
                            Operator: BinaryOperationType.Dereference,
                            Right: CompileTimeValue
                        }) {
                        throw new CompilationFailureException("Inline assembly output must be a variable reference (var:size).");
                    }
                }
                foreach (string clobber in ilasm.Clobbers) {
                    ValidateRegister(clobber);
                }
                break;
            }

            case ReturnStatement retStmt: {
                if (retStmt.Value != null) {
                    AnalyseExpression(retStmt.Value);
                }
                break;
            }
            
            case FunctionCall fc: {
                AnalyseExpression(fc);
                break;
            }

            default: {
                throw new NotImplementedException($"Statement analysis not implemented yet for '{statement.GetType().Name}'.");
            }
        }
    }

    private void AnalyseExpression(IValueExpression expr) {
        while (true) {
            switch (expr) {
                // these are fine as-is
                case IntegerLiteral:
                case StringLiteral:
                    break;
                
                case StructSizeof sso: {
                    _neededStructs.Add((sso.StructName, null));
                    break;
                }
                
                case StructOffsetOf sof: {
                    _neededStructs.Add((sof.StructName, sof.ParamName));
                    break;
                }
                
                case VariableToken vt: {
                    // TODO: check existence?
                    break;
                }

                case BinaryOperation bo: {
                    AnalyseExpression(bo.Left);
                    expr = bo.Right;
                    continue;
                }

                case UnaryOperation uo: {
                    expr = uo.Operand;
                    continue;
                }

                case FunctionCall fc: {
                    foreach (IValueExpression arg in fc.Arguments) {
                        AnalyseExpression(arg);
                    }
                    
                    if (fc.Target is VariableToken vt) {
                        if (_locals.Contains(vt.Name)) {
                            break;  // calling a local variable, can't validate further
                        }
                        _functionCalls.Add((vt.Name, fc.Arguments));
                    }
                    break;
                }
            }

            break;
        }
    }
}
