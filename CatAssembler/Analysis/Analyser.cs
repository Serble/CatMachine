using System.Diagnostics.CodeAnalysis;
using System.Text;
using CatAssembler.Assembler;
using CatAssembler.Exceptions;
using CatAssembler.Parser;
using Expression = NCalc.Expression;

namespace CatAssembler.Analysis;

public class Analyser {
    private const int MaxVariableDepth = 64;
    private const string AllChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    
    private readonly Stack<Token> _tokens = [];
    private readonly Dictionary<string, Macro> _macros = new();

    public Analyser(Token[] tokens) {
        for (int i = tokens.Length - 1; i >= 0; i--) {
            _tokens.Push(tokens[i]);
        }
    }

    public (IOutputSegment[] segments, Dictionary<string, string> constants, DebugTable debugSymbols) Analyse() {
        int filePos = 0;
        Dictionary<string, (string expr, string file, int line)> constants = [];
        List<DebugSymbol> debugSymbols = [];

        List<IOutputSegment> segments = [];
        
        while (_tokens.TryPop(out Token? token)) {
            switch (token) {
                case LabelToken label: {
                    if (constants.ContainsKey(label.Name)) {
                        Fail(token, "Constant already defined: " + label.Name);
                    }
                    constants.Add(label.Name, ($"{filePos}", label.File, label.Line));
                    break;
                }
                
                case DirectiveToken directive: {
                    switch (directive.Name) {
                        case "define" or "const": {
                            AssertArgCount(directive, 2);
                            string name = AssertExpression<NameExpression>(directive, directive.Args[0]).Value;
                            NumberExpression valueExpr = AssertNumberExpression(directive, directive.Args[1]);
                            if (constants.ContainsKey(name)) {
                                Fail(directive, "Constant already defined: " + name);
                            }
                            constants.Add(name, (valueExpr.Value, directive.File, directive.Line));
                            break;
                        }

                        case "macro": {
                            AssertArgCount(directive, 3);
                            
                            string name = AssertExpression<NameExpression>(directive, directive.Args[0]).Value;
                            NumberExpression argCountExpr = AssertNumberExpression(token, directive.Args[1]);
                            Dictionary<string, string> mod = CompactConstants();
                            string argCountName = Guid.NewGuid().ToString();
                            mod.Add(argCountName, argCountExpr.Value);
                            int argCount;
                            
                            try {
                                argCount = (int)EvaluateVariable(argCountName, mod);
                            }
                            catch (Exception e) when (e is CircularDependencyException or KeyNotFoundException or InvalidOperationException) {
                                throw Fail(token, "Macro argument count must be a constant integer resolvable at first pass" + e);
                            }

                            MacroBodyExpression lines = AssertExpression<MacroBodyExpression>(directive, directive.Args[2]);
                            
                            _macros.Add(name, new Macro(lines.Value, argCount, lines.LineNumber));
                            break;
                        }

                        case "endmacro": {
                            Fail(token, "Cannot have an endmacro outside of a macro");
                            break;
                        }

                        case "include": {
                            AssertArgCount(directive, 1);
                            string file = directive.Args[0] switch {
                                NameExpression name => name.Value,
                                StringExpression str => str.Value,
                                _ => throw Fail(directive, $"Include directive requires a string or name expression " +
                                                           $"as argument, got: {directive.Args[0].GetType().FullName}")
                            };

                            string fullPath = ProcessIncludePath(directive.File, file);
                            if (!File.Exists(fullPath)) {
                                Fail(directive, "Included file not found: " + file);
                            }

                            string[] content = File.ReadAllLines(fullPath);
                            Tokeniser tokeniser = new(file, content);
                            Token[] newTokens = tokeniser.Tokenise();
                            for (int i = newTokens.Length - 1; i >= 0; i--) {
                                _tokens.Push(newTokens[i]);
                            }
                            break;
                        }

                        default: {
                            throw new ParseException(directive.File, directive.Line, $"Unknown directive: {directive.Name}");
                        }
                    }
                    break;
                }

                case InstructionToken instruction: {
                    debugSymbols.Add(new DebugSymbol(filePos, instruction.Line, instruction.Raw));
                    
                    if (_macros.TryGetValue(instruction.Name, out Macro? macro)) {
                        AssertArgCount(instruction, macro.ArgCount);
                        
                        StringBuilder expansionIdBuilder = new();
                        for (int i = 0; i < 16; i++) {
                            expansionIdBuilder.Append(AllChars[Random.Shared.Next(AllChars.Length)]);
                        }

                        string expansionId = expansionIdBuilder.ToString();
                        
                        string[] lines = macro.Lines.Select(line => {
                            // reverse order so $10 isn't replaced with $1's replacement
                            for (int i = macro.ArgCount; i >= 1; i--) {
                                line = line.Replace($"${i}", instruction.Args[i - 1].RawValue);
                            }
                            
                            line = line.Replace("$0", expansionId);
                            return line;
                        }).ToArray();
                        
                        Tokeniser tokeniser = new(instruction.File, lines, macro.LineNumber);
                        Token[] newTokens = tokeniser.Tokenise();
                        for (int i = newTokens.Length - 1; i >= 0; i--) {
                            _tokens.Push(newTokens[i]);
                        }
                        break;
                    }
                    
                    // custom instructions
                    IOutputSegment? customInstr = FindCustomInstruction(instruction);
                    if (customInstr != null) {
                        customInstr = customInstr.Copy();
                        if (customInstr is ArgumentOutputSegment argSeg) {
                            if (!argSeg.ValidateArgs(instruction, instruction.Args, CompactConstants(), out string? customError)) {
                                Fail(instruction, $"Invalid arguments for custom instruction {instruction.Name}: {customError}");
                            }
                        }
                        filePos += customInstr.SizeInBytes;
                        segments.Add(customInstr);
                        break;
                    }
                    
                    // regular instruction
                    InstructionSpec? spec = FindInstruction(instruction);
                    if (spec == null) {
                        Fail(instruction, $"Unknown instruction or invalid arguments: {instruction.Name}, args: " +
                                           $"{string.Join(", ", instruction.Args.Select(a => a.GetType().Name))}");
                    }

                    filePos += 1 + spec.ArgTypes.Sum(t => t.type.SizeInBytes);
                    EncodableInstruction segment = new(spec.Id, spec.ArgTypes.Select(a => a.type).ToArray());

                    if (!segment.ValidateArgs(instruction, instruction.Args, CompactConstants(), out string? error)) {
                        Fail(instruction, $"Invalid arguments for instruction {instruction.Name}: {error}");
                    }
                    segments.Add(segment);
                    break;
                }
            }
        }
        
        
        
        Console.WriteLine("Analysis complete. Debug symbols generated. Generated " +
                          $"{segments.Count} segments, " +
                          $"{constants.Count} constants, " +
                          $"{filePos} bytes.");

        Dictionary<string, string> finalExpressions = CompactConstants();
        return (segments.ToArray(), CompactConstants(), new DebugTable(debugSymbols.ToArray(), constants.ToDictionary(
            kv => kv.Key,
            kv => EvaluateVariable(kv.Key, finalExpressions)
        )));

        Dictionary<string, string> CompactConstants() => constants.ToDictionary(kv => kv.Key, kv => kv.Value.expr);
    }
    
    private NumberExpression AssertNumberExpression(Token token, IExpression expr) {
        if (expr is StringExpression) {
            Fail(token, "String expression not valid here");
        }

        if (expr is RegisterExpression) {
            Fail(token, "Register expression not valid here");
        }

        if (expr is NameExpression name) {
            return name.ToNumber();
        }
        
        return AssertExpression<NumberExpression>(token, expr);
    }

    private void AssertArgCount(DirectiveToken directive, int argCount) {
        if (directive.Args.Length != argCount) {
            Fail(directive, $"Directive {directive.Name} expects {argCount} arguments, got {directive.Args.Length}");
        }
    }
    
    private void AssertArgCount(InstructionToken directive, int argCount) {
        if (directive.Args.Length != argCount) {
            Fail(directive, $"Directive {directive.Name} expects {argCount} arguments, got {directive.Args.Length}");
        }
    }
    
    private T AssertExpression<T>(Token token, IExpression expr) where T : IExpression {
        if (expr is not T t) {
            Fail(token, $"Expected expression of type {typeof(T).Name}, got {expr.GetType().Name}");
            throw new Exception();  // unreachable
        }
        return t;
    }

    private InstructionSpec? FindInstruction(InstructionToken token) {
        if (token.Args.Any(e => e is StringExpression)) {
            Fail(token, "String expressions are not valid instruction arguments");
        }
        
        InstructionSpec? instruction = Spec.Instructions.FirstOrDefault(i => 
            i.Mneumonics.Contains(token.Name) && 
            ArgsMatchExpressions(i, token.Args));
        
        return instruction;
    }
    
    private IOutputSegment? FindCustomInstruction(InstructionToken token) {
        (string[] mneumonics, IOutputSegment segment) = Spec.CustomInstructions.FirstOrDefault(i => 
            i.Mneumonics.Contains(token.Name));
        
        return segment;
    }
    
    private static bool ArgsMatchExpressions(InstructionSpec spec, IExpression[] args) {
        if (spec.ArgTypes.Length != args.Length) {
            return false;
        }

        for (int i = 0; i < args.Length; i++) {
            if (spec.ArgTypes[i].mem != args[i] is IPointerCapableExpression { Pointer: true }) {
                return false;
            }

            if (spec.ArgTypes[i].type is RegisterType) {
                if (args[i] is not RegisterExpression) {
                    return false;
                }
                continue;
            }
            
            // Immediate type
            if (args[i] is not NumberExpression && args[i] is not NameExpression) {
                return false;
            }
        }

        return true;
    }
    
    private static string NCalcCleanExpression(string expr) {
        return expr;  // for future use
    }
    
    public static uint EvaluateVariable(string varName, Dictionary<string, string> expressions, int depth = 1) {
        Dictionary<string, string> cleaned = expressions.ToDictionary(dirty => 
            NCalcCleanExpression(dirty.Key), dirty => NCalcCleanExpression(dirty.Value));

        if (!cleaned.TryGetValue(varName, out string? expression)) {
            throw new KeyNotFoundException(varName);
        }
        Expression expr = new(expression);
        
        // Prevent infinite recursion
        if (depth > MaxVariableDepth) {
            throw new CircularDependencyException();
        }

        // NCalc lets you provide a delegate to resolve variables at evaluation time
        expr.EvaluateParameter += (name, args) => {
            args.Result = EvaluateVariable(name, cleaned, depth + 1);
        };

        object result = expr.Evaluate();

        return result switch {
            uint u => u,
            int i => (uint)i,
            long l => (uint)l,
            double d => (uint)d,
            decimal d => (uint)d,
            float s => (uint)s,
            bool b => b ? (uint)1 : 0,
            string s => throw new InvalidOperationException(
                $"Expression evaluated to a string, which is not supported: {s}, {expression}"),
            _ => throw new InvalidOperationException(
                $"Expression evaluated to unsupported type: {result.GetType().FullName}")
        };
    }

    // returns exception for convenience in expression-bodied methods
    [DoesNotReturn]
    public static Exception Fail(Token token, string msg) {
        throw new ParseException(token.File, token.Line, msg);
    }

    /// <summary>
    /// Converts the new include file path to a path based on the current file's location.
    /// Or if the new file path is already absolute, returns it as is.
    /// </summary>
    /// <param name="currentFile">Maybe a relative path, just a file name, or an absolute path.</param>
    /// <param name="newFile">The new file information.</param>
    /// <returns>The modified path.</returns>
    public static string ProcessIncludePath(string currentFile, string newFile) {
        if (Path.IsPathRooted(newFile)) {
            return newFile;
        }

        string? currentDir = Path.GetDirectoryName(Path.GetFullPath(currentFile));
        if (currentDir == null) {
            return newFile;
        }

        return Path.Combine(currentDir, newFile);
    }
}
