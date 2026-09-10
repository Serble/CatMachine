using System.Globalization;

namespace CatVM.DebugAdapter;

/// <summary>What an expression can look at.</summary>
public interface IEvalContext {
    bool TryGetRegister(string name, out uint value);
    bool TryGetLabel(string name, out uint value);
    uint ReadMemory(uint address, int size);
}

/// <summary>
/// Result of evaluating an expression. <paramref name="Size"/> is 4 unless the outermost
/// operation was a sized memory read; <paramref name="MemoryReference"/> is set when the value
/// names a place in memory (a label, a dereference, <c>sp</c>/<c>ip</c>).
/// </summary>
public readonly record struct EvalResult(uint Value, int Size, string? MemoryReference) {
    public string Type => Size switch { 1 => "uint8", 2 => "uint16", _ => "uint32" };

    public string Display => Size switch {
        1 => $"0x{Value:X2} ({Value})",
        2 => $"0x{Value:X4} ({Value})",
        _ => $"0x{Value:X8} ({Value})"
    };
}

public sealed class EvalException(string message) : Exception(message);

/// <summary>
/// A small expression language for watches and hovers:
/// <code>
/// expr := term (('+' | '-') term)*
/// term := atom ('*' atom)*
/// atom := register | label | number | '(' expr ')' | '-' atom | '[' expr ']' (':' size)?
/// </code>
/// Registers are <c>r0..r7</c>, <c>sp</c>, <c>ip</c>, <c>fl</c> (optionally prefixed with
/// <c>$</c>); numbers are decimal, <c>0x</c> hex or <c>0b</c> binary; <c>[x]</c> reads a
/// 32-bit word at <c>x</c>, <c>[x]:1</c> / <c>[x]:2</c> read a byte or half-word.
/// </summary>
public sealed class ExpressionEvaluator {
    private static readonly HashSet<string> Registers = ["r0", "r1", "r2", "r3", "r4", "r5", "r6", "r7", "sp", "ip", "fl"];

    public EvalResult Evaluate(string expression, IEvalContext context) {
        Parser parser = new(expression, context);
        EvalResult result = parser.ParseExpression();
        parser.ExpectEnd();
        return result;
    }

    private sealed class Parser(string text, IEvalContext context) {
        private int _pos;

        private void SkipSpaces() {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos])) _pos++;
        }

        private char Peek() {
            SkipSpaces();
            return _pos < text.Length ? text[_pos] : '\0';
        }

        private bool Accept(char c) {
            if (Peek() != c) return false;
            _pos++;
            return true;
        }

        public void ExpectEnd() {
            if (Peek() != '\0') throw new EvalException($"Unexpected '{text[_pos]}' at position {_pos + 1}");
        }

        public EvalResult ParseExpression() {
            EvalResult left = ParseTerm();
            while (true) {
                if (Accept('+')) {
                    EvalResult right = ParseTerm();
                    left = new EvalResult(unchecked(left.Value + right.Value), 4, null);
                }
                else if (Accept('-')) {
                    EvalResult right = ParseTerm();
                    left = new EvalResult(unchecked(left.Value - right.Value), 4, null);
                }
                else {
                    return left;
                }
            }
        }

        private EvalResult ParseTerm() {
            EvalResult left = ParseAtom();
            while (Accept('*')) {
                EvalResult right = ParseAtom();
                left = new EvalResult(unchecked(left.Value * right.Value), 4, null);
            }

            return left;
        }

        private EvalResult ParseAtom() {
            char c = Peek();
            switch (c) {
                case '\0':
                    throw new EvalException("Unexpected end of expression");

                case '(': {
                    _pos++;
                    EvalResult inner = ParseExpression();
                    if (!Accept(')')) throw new EvalException("Expected ')'");
                    return inner;
                }

                case '-': {
                    _pos++;
                    EvalResult inner = ParseAtom();
                    return new EvalResult(unchecked(0u - inner.Value), 4, null);
                }

                case '[': {
                    _pos++;
                    EvalResult address = ParseExpression();
                    if (!Accept(']')) throw new EvalException("Expected ']'");
                    int size = 4;
                    if (Accept(':')) {
                        string sizeText = ReadWord();
                        size = sizeText switch {
                            "1" => 1,
                            "2" => 2,
                            "4" => 4,
                            _ => throw new EvalException($"Size must be 1, 2 or 4, not '{sizeText}'")
                        };
                    }

                    uint value;
                    try {
                        value = context.ReadMemory(address.Value, size);
                    }
                    catch (Exception e) {
                        throw new EvalException($"Cannot read {size} byte(s) at 0x{address.Value:X8}: {e.Message}");
                    }

                    return new EvalResult(value, size, MemoryReferences.Encode(address.Value));
                }
            }

            if (char.IsDigit(c)) {
                return ParseNumber();
            }

            if (char.IsLetter(c) || c is '_' or '$' or '.') {
                string word = ReadWord();
                string lookup = word.TrimStart('$');
                if (Registers.Contains(lookup.ToLowerInvariant())) {
                    if (!context.TryGetRegister(lookup.ToLowerInvariant(), out uint reg)) {
                        throw new EvalException($"Unknown register '{word}'");
                    }

                    string? reference = lookup.ToLowerInvariant() is "sp" or "ip" ? MemoryReferences.Encode(reg) : null;
                    return new EvalResult(reg, 4, reference);
                }

                if (context.TryGetLabel(word, out uint label)) {
                    return new EvalResult(label, 4, MemoryReferences.Encode(label));
                }

                throw new EvalException($"Unknown symbol '{word}'");
            }

            throw new EvalException($"Unexpected '{c}' at position {_pos + 1}");
        }

        private string ReadWord() {
            SkipSpaces();
            int start = _pos;
            while (_pos < text.Length && (char.IsLetterOrDigit(text[_pos]) || text[_pos] is '_' or '$' or '.')) _pos++;
            if (start == _pos) throw new EvalException($"Expected a name at position {_pos + 1}");
            return text[start.._pos];
        }

        private EvalResult ParseNumber() {
            string word = ReadWord();
            uint value;
            bool ok;
            if (word.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
                ok = uint.TryParse(word[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }
            else if (word.StartsWith("0b", StringComparison.OrdinalIgnoreCase)) {
                try {
                    value = Convert.ToUInt32(word[2..], 2);
                    ok = word.Length > 2;
                }
                catch (Exception) {
                    value = 0;
                    ok = false;
                }
            }
            else {
                ok = uint.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out value);
            }

            if (!ok) throw new EvalException($"'{word}' is not a number");
            return new EvalResult(value, 4, null);
        }
    }
}

/// <summary>Evaluation context over a live VM and its symbols.</summary>
public sealed class VmEvalContext(CatVm vm, Debugger.DebugTableIndex index) : IEvalContext {
    public bool TryGetRegister(string name, out uint value) {
        if (CatData.RegisterExtensions.TryParse(name, out CatData.Register register)) {
            value = vm.Cpu.Get((byte)register);
            return true;
        }

        value = 0;
        return false;
    }

    public bool TryGetLabel(string name, out uint value) {
        return index.TryGetLabel(name, out value);
    }

    public uint ReadMemory(uint address, int size) {
        return size switch {
            1 => vm.Read8(address),
            2 => vm.Read16(address),
            _ => vm.ReadWord(address)
        };
    }
}
