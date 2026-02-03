using System.Numerics;
using Sprache;

namespace IntegerMaths;

public class Expression(string text) {
    public Dictionary<string, string> Variables { get; } = new();
    public event Action<string, EvaluateVariableEventArgs>? EvaluateVariableEvent;
    
    public class EvaluateVariableEventArgs(string name) : EventArgs {
        public string Name { get; } = name;
        public BigInteger? Value { get; set; }
    }
    
    public BigInteger Eval() {
        return Eval(text);
    }
    
    public uint EvalAsUInt() {
        return Eval().ToUInt32WithOverflow();
    }

    private BigInteger Eval(string exprText) {
        Expr expr = MathsParser.Expression.End().Parse(exprText);
        return Eval(expr);
    }

    private BigInteger Eval(Expr expr) {
        switch (expr) {
            case Literal l:
                return l.Value;
            
            case Binary b:
                BigInteger left = Eval(b.Left);
                BigInteger right = Eval(b.Right);
                
                return b.Op switch {
                    "+" => left + right,
                    "-" => left - right,
                    "*" => left * right,
                    "/" => left / right,
                    "%" => left % right,
                    "&" => left & right,
                    "|" => left | right,
                    "^" => left ^ right,
                    "<<" => left << (int)right,
                    ">>" => left >> (int)right,
                    _ => throw new InvalidOperationException($"Unknown operator {b.Op}"),
                };
                
            case Variable v:
                if (Variables.TryGetValue(v.Name, out string? val)) {
                    return Eval(val);
                }
                
                EvaluateVariableEventArgs args = new(v.Name);
                EvaluateVariableEvent?.Invoke(v.Name, args);

                return args.Value ?? throw new Exception($"Variable {v.Name} not defined");
            
            default:
                throw new InvalidOperationException("Unknown expression type");
        }
    }
}
