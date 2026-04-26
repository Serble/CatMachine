using System.Text;
using CatAssembler.Analysis;
using CatAssembler.Exceptions;
using CatAssembler.Parser;
using CatData;

namespace CatAssembler.Assembler;

public interface IOutputSegment {
    int SizeInBytes { get; }
    byte[] GetBytes(Dictionary<string, string> constants);
    IOutputSegment Copy();
}

public record OutputSegment(byte[] Bytes) : IOutputSegment {
    public int SizeInBytes => Bytes.Length;

    public byte[] GetBytes(Dictionary<string, string> _) => Bytes;
    public IOutputSegment Copy() {
        return this with { };
    }
}

public abstract record ArgumentOutputSegment : IOutputSegment {
    public abstract int SizeInBytes { get; }
    /// <summary>
    /// Whether to automatically validate expressions in arguments.
    /// Set to false for instructions that handle their own expression validation.
    /// Or if they need string arguments, etc.
    /// </summary>
    public virtual bool PerformExpressionValidation => true;
    public abstract bool ValidateArgs(InstructionToken token, IExpression[] args, 
        Dictionary<string, string> prelimConstants, out string? error);
    public abstract byte[] GetBytes(Dictionary<string, string> constants);
    public IOutputSegment Copy() {
        return this with { };
    }
}

public record ReserveInstruction(int Bytes) : ArgumentOutputSegment {
    public override int SizeInBytes => _sizeInBytes;
    private int _sizeInBytes = -1;
    
    public override bool ValidateArgs(InstructionToken _, IExpression[] args, 
        Dictionary<string, string> prelimConstants, out string? error) {
        if (args.Length != 1) {
            error = "Res instruction requires exactly one argument.";
            return false;
        }

        NumberExpression arg;
        switch (args[0]) {
            case NameExpression name:
                arg = name.ToNumber();
                break;
            case NumberExpression number:
                arg = number;
                break;
            default:
                error = "Res instruction argument must be a number.";
                return false;
        }

        Dictionary<string, string> mod = new(prelimConstants);
        string tempVar = Guid.NewGuid().ToString();
        mod.Add(tempVar, arg.Value);
        uint size;
        try {
            size = Analyser.EvaluateVariable(tempVar, mod);
        }
        catch (KeyNotFoundException e) {
            error =
                $"Undefined variable in Res instruction argument '{e.Message}', expression must be resolvable at first pass.";
            return false;
        }
        catch (CircularDependencyException) {
            error = "Circular dependency detected in Res instruction argument, expression must be resolvable at first pass.";
            return false;
        }
        
        long sizeInBytes = size * Bytes;
        if (sizeInBytes > int.MaxValue) {
            error = $"Res instruction size exceeds maximum allowed size ({sizeInBytes}).";
            return false;
        }
        
        _sizeInBytes = (int)sizeInBytes;
        error = null;
        return true;
    }

    public override byte[] GetBytes(Dictionary<string, string> _) {
        return SizeInBytes == -1 ? 
            throw new InvalidOperationException("Cannot get bytes of unvalidated ResInstruction") : 
            new byte[SizeInBytes];
    }
}

public record DefineInstruction(int BytesPerEntry) : ArgumentOutputSegment {
    public override int SizeInBytes => BytesPerEntry * _entryCount;
    private int _entryCount = -1;
    private IExpression[] _args = [];
    
    // here we'll just work out how many entries there are
    // and validate that all args are numbers
    public override bool ValidateArgs(InstructionToken token, IExpression[] args, 
        Dictionary<string, string> _, out string? error) {
        _entryCount = args.Length;
        if (args.Any(arg => arg is not (NumberExpression or NameExpression))) {
            error = "Define instruction arguments must be numbers.";
            return false;
        }
        
        _args = args;
        error = null;
        return true;
    }

    // then here we'll actually generate the bytes
    // by evaluating each argument
    public override byte[] GetBytes(Dictionary<string, string> constants) {
        List<uint> entries = [];
        foreach (IExpression arg in _args) {
            NumberExpression num = arg as NumberExpression ?? ((NameExpression)arg).ToNumber();
            
            string tempVar = Guid.NewGuid().ToString();
            Dictionary<string, string> mod = new(constants) { { tempVar, num.Value } };
            uint value;
            try {
                value = Analyser.EvaluateVariable(tempVar, mod);
            }
            catch (KeyNotFoundException e) {
                throw new InvalidOperationException(
                    $"Undefined variable in Define instruction argument '{e.Message}'.");
            }
            catch (CircularDependencyException) {
                throw new InvalidOperationException("Circular dependency detected in Define instruction argument.");
            }
            
            entries.Add(value);
        }
        
        List<byte> result = [];
        foreach (byte[] entryBytes in entries.Select(entry => BytesPerEntry switch {
                     1 => [(byte)entry],
                     2 => BitConverter.GetBytes((ushort)entry),
                     4 => BitConverter.GetBytes(entry),
                     _ => throw new ArgumentException("BytesPerEntry must be 1, 2, or 4")
                 })) {
            result.AddRange(entryBytes);
        }
        return result.ToArray();
    }
}

public record DirectFileInstruction : ArgumentOutputSegment {
    public override int SizeInBytes => _fileBytes.Length;
    public override bool PerformExpressionValidation => false;
    private byte[] _fileBytes = [];
    
    public override bool ValidateArgs(InstructionToken token, IExpression[] args, Dictionary<string, string> prelimConstants, out string? error) {
        if (args.Length != 1) {
            error = "DirectFileInstruction requires exactly one argument.";
            return false;
        }

        string filePath;
        switch (args[0]) {
            case StringExpression strExpr:
                filePath = strExpr.Value;
                break;
            case NameExpression nameExpr:
                filePath = nameExpr.Value;
                break;
            default:
                error = "DirectFileInstruction argument must be a string or name expression.";
                return false;
        }

        try {
            filePath = Analyser.ProcessIncludePath(token.File, filePath);
            _fileBytes = File.ReadAllBytes(filePath);
        }
        catch (Exception e) {
            error = $"Failed to read file '{filePath}': {e.Message}";
            return false;
        }

        error = null;
        return true;
    }

    public override byte[] GetBytes(Dictionary<string, string> constants) {
        return _fileBytes;
    }
}

public record DirectStringInstruction : ArgumentOutputSegment {
    public override int SizeInBytes => _stringBytes.Length;
    public override bool PerformExpressionValidation => false;
    private byte[] _stringBytes = [];
    
    public override bool ValidateArgs(InstructionToken token, IExpression[] args, Dictionary<string, string> prelimConstants, out string? error) {
        if (args.Length != 1) {
            error = "DirectStringInstruction requires exactly one argument.";
            return false;
        }

        string strValue;
        switch (args[0]) {
            case StringExpression strExpr:
                strValue = strExpr.Value;
                break;
            case NameExpression nameExpr:
                strValue = nameExpr.Value;
                break;
            default:
                error = "DirectStringInstruction argument must be a string or name expression.";
                return false;
        }

        _stringBytes = Encoding.UTF8.GetBytes(strValue);
        error = null;
        return true;
    }

    public override byte[] GetBytes(Dictionary<string, string> constants) {
        return _stringBytes;
    }
}

public record EncodableInstruction(byte OpCode, IInstructionArgType[] ArgTypes) : ArgumentOutputSegment {
    public override int SizeInBytes => 1 + ArgTypes.Sum(t => t.SizeInBytes);
    private IExpression[] _args = [];
    
    public override bool ValidateArgs(InstructionToken token, IExpression[] args, 
        Dictionary<string, string> prelimConstants, out string? error) {
        // args are already validate when it tries to find the instruction
        _args = args;
        error = null;
        return true;
    }

    public override byte[] GetBytes(Dictionary<string, string> constants) {
        List<byte> result = [OpCode];
        for (int i = 0; i < ArgTypes.Length; i++) {
            if (ArgTypes[i] is RegisterType regType) {
                result.AddRange((regType with {
                    Value = ((RegisterExpression)_args[i]).Value
                }).GetBytes());
                continue;
            }
            
            NumberExpression value = _args[i] as NumberExpression ?? ((NameExpression)_args[i]).ToNumber();
            
            string tempVar = Guid.NewGuid().ToString();
            Dictionary<string, string> mod = new(constants) { { tempVar, value.Value } };
            uint evaluatedValue;
            try {
                evaluatedValue = Analyser.EvaluateVariable(tempVar, mod);
            }
            catch (KeyNotFoundException e) {
                throw new InvalidOperationException(
                    $"Undefined variable in instruction argument '{e.Message}'.");
            }
            catch (CircularDependencyException) {
                throw new InvalidOperationException("Circular dependency detected in instruction argument.");
            }

            ImmediateType imm = (ImmediateType)ArgTypes[i];
            imm = imm.WithValue(evaluatedValue);
            result.AddRange(imm.GetBytes());
        }
        
        return result.ToArray();
    }
}

// This is to support
// jmp <imm32>
// and jmp <reg>
// and not just jmp <reg> <imm32>
// but because it will effectively overwrite the existing instruction
// we have to support 2 args as well
public record JumpStyleInstruction(byte OpCode) : ArgumentOutputSegment {
    public override int SizeInBytes => 6;  // opcode + reg(1) + imm32(4)
    private Register? _register;
    private NumberExpression _offset = new("0", "0", false);
    
    public override bool ValidateArgs(InstructionToken token, IExpression[] args, 
        Dictionary<string, string> _, out string? error) {
        switch (args.Length) {
            case 1 when args[0] is RegisterExpression regExpr:
                _register = regExpr.Value;
                error = null;
                return true;
            
            case 1 when args[0] is NumberExpression numExpr:
                _offset = numExpr;
                error = null;
                return true;
            
            case 1 when args[0] is NameExpression nameExpr:
                _offset = nameExpr.ToNumber();
                error = null;
                return true;
            
            case 2 when args[0] is RegisterExpression regExpr &&
                        args[1] is NumberExpression or NameExpression:
                _register = regExpr.Value;
                _offset = args[1] as NumberExpression ?? ((NameExpression)args[1]).ToNumber();
                error = null;
                return true;
            
            default:
                error = "JumpStyleInstruction requires either one register argument, one number argument, or both a register and a number argument.";
                return false;
        }
    }

    public override byte[] GetBytes(Dictionary<string, string> constants) {
        List<byte> result = [OpCode];
        
        if (_register.HasValue) {
            result.Add((byte)_register.Value);
        }
        else {
            result.Add(0xFF);  // no register
        }
        
        string tempVar = Guid.NewGuid().ToString();
        Dictionary<string, string> mod = new(constants) { { tempVar, _offset.Value } };
        uint evaluatedValue;
        try {
            evaluatedValue = Analyser.EvaluateVariable(tempVar, mod);
        }
        catch (KeyNotFoundException e) {
            throw new InvalidOperationException(
                $"Undefined variable in jump instruction offset '{e.Message}', {_offset.Value}.");
        }
        catch (CircularDependencyException) {
            throw new InvalidOperationException("Circular dependency detected in jump instruction offset.");
        }

        result.AddRange(new Immediate32Type(evaluatedValue).GetBytes());
        return result.ToArray();
    }
}
