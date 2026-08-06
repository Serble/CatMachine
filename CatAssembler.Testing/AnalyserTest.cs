using CatAssembler.Analysis;
using CatAssembler.Assembler;
using CatAssembler.Exceptions;
using CatAssembler.Parser;
using CatData;

namespace CatAssembler.Testing;

/// <summary>
/// Tests for <see cref="Analyser"/>: constant resolution, label positions,
/// macro expansion, expression evaluation, and error detection.
/// </summary>
[TestFixture]
public class AnalyserTest {

    private static (IOutputSegment[] segments, Dictionary<string, string> constants, DebugTable debugSymbols)
        Analyse(string source) {
        Token[] tokens = new Tokeniser("test.asm", source).Tokenise();
        Analyser analyser = new(tokens);
        return analyser.Analyse();
    }

    // ── Constants / #define / #const ─────────────────────────────────────────

    [Test]
    public void Define_AddsConstantResolvableByName() {
        (_, Dictionary<string, string> constants, _) = Analyse("#define FOO, 42");
        Assert.That(constants.ContainsKey("FOO"), Is.True);
        Assert.That(Analyser.EvaluateVariable("FOO", constants), Is.EqualTo(42u));
    }

    [Test]
    public void Const_IsAliasForDefine() {
        (_, Dictionary<string, string> constants, _) = Analyse("#const BAR, 0xFF");
        Assert.That(Analyser.EvaluateVariable("BAR", constants), Is.EqualTo(0xFFu));
    }

    [Test]
    public void Define_DuplicateName_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Analyse("""
            #define FOO, 1
            #define FOO, 2
            """));
    }

    [Test]
    public void Define_ExpressionValue_Evaluates() {
        (_, Dictionary<string, string> constants, _) = Analyse("#define VAL, (3+4)");
        Assert.That(Analyser.EvaluateVariable("VAL", constants), Is.EqualTo(7u));
    }

    // ── Labels as position constants ─────────────────────────────────────────

    [Test]
    public void Label_BeforeAnyInstruction_HasPositionZero() {
        (_, Dictionary<string, string> constants, _) = Analyse("""
            myLabel:
            nop
            """);
        Assert.That(constants.ContainsKey("myLabel"), Is.True);
        Assert.That(Analyser.EvaluateVariable("myLabel", constants), Is.EqualTo(0u));
    }

    [Test]
    public void Label_AfterNop_HasPositionOne() {
        // nop encodes to 1 byte
        (_, Dictionary<string, string> constants, _) = Analyse("""
            nop
            after:
            """);
        Assert.That(Analyser.EvaluateVariable("after", constants), Is.EqualTo(1u));
    }

    [Test]
    public void Label_AfterMovReg_ReflectsInstructionSize() {
        // mov R0, R1 → opcode(1) + reg(1) + reg(1) = 3 bytes
        (_, Dictionary<string, string> constants, _) = Analyse("""
            mov R0, R1
            here:
            """);
        Assert.That(Analyser.EvaluateVariable("here", constants), Is.EqualTo(3u));
    }

    [Test]
    public void LocalLabel_ScopedToGlobalLabel() {
        (_, Dictionary<string, string> constants, _) = Analyse("""
            outer:
            .inner:
            nop
            """);
        string expectedKey = $"{Tokeniser.LocalLabelPrefix}outer__inner";
        Assert.That(constants.ContainsKey(expectedKey), Is.True);
        Assert.That(Analyser.EvaluateVariable(expectedKey, constants), Is.EqualTo(0u));
    }

    [Test]
    public void DuplicateLabel_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Analyse("""
            same:
            same:
            """));
    }

    // ── Macro expansion ───────────────────────────────────────────────────────

    [Test]
    public void Macro_ZeroArgs_ExpandsInline() {
        // A macro with no args that emits a nop. nop = 1 byte, so after == 1.
        (_, Dictionary<string, string> constants, _) = Analyse("""
            #macro mynop, 0
            nop
            #endmacro
            mynop
            after:
            """);
        Assert.That(Analyser.EvaluateVariable("after", constants), Is.EqualTo(1u));
    }

    [Test]
    public void Macro_WithArgs_SubstitutesArgValues() {
        // A 1-arg macro that emits: add R0, $1
        // After expansion we should have 1 instruction add R0, 5  → 1+1+4 = 6 bytes
        (_, Dictionary<string, string> constants, _) = Analyse("""
            #macro incr, 1
            add R0, $1
            #endmacro
            incr 5
            after:
            """);
        Assert.That(Analyser.EvaluateVariable("after", constants), Is.EqualTo(6u));
    }

    [Test]
    public void Macro_MultipleArgs_CorrectSubstitution() {
        // 2-arg macro: mov $1, $2
        // mov R0, R1 → 3 bytes
        (_, Dictionary<string, string> constants, _) = Analyse("""
            #macro movxy, 2
            mov $1, $2
            #endmacro
            movxy R0, R1
            after:
            """);
        Assert.That(Analyser.EvaluateVariable("after", constants), Is.EqualTo(3u));
    }

    [Test]
    public void Macro_LocalLabels_GetUniqueExpansionId() {
        // Each invocation gets a unique $0 substitution, so two calls don't produce duplicate labels
        Assert.DoesNotThrow(() => Analyse("""
            #macro withlocal, 0
            $0_label:
            nop
            #endmacro
            withlocal
            withlocal
            """));
    }

    [Test]
    public void Macro_EndmacroOutsideMacro_ThrowsParseException() {
        // The tokeniser reads the endmacro as part of the macro; if we feed the analyser
        // a raw endmacro directive it should fail.
        Assert.Throws<ParseException>(() => {
            Token[] tokens = [new DirectiveToken("#endmacro", "endmacro", [], "test.asm", 1)];
            new Analyser(tokens).Analyse();
        });
    }

    // ── Expression evaluation ────────────────────────────────────────────────

    [Test]
    public void EvaluateVariable_SimpleNumber_Evaluates() {
        Assert.That(Analyser.EvaluateVariable("x", new Dictionary<string, string> { ["x"] = "7" }), Is.EqualTo(7u));
    }

    [Test]
    public void EvaluateVariable_HexLiteral_Evaluates() {
        Assert.That(Analyser.EvaluateVariable("x", new Dictionary<string, string> { ["x"] = "0x10" }), Is.EqualTo(16u));
    }

    [Test]
    public void EvaluateVariable_ArithmeticExpression_Evaluates() {
        Assert.That(Analyser.EvaluateVariable("x", new Dictionary<string, string> { ["x"] = "2 + 3 * 4" }), Is.EqualTo(14u));
    }

    [Test]
    public void EvaluateVariable_ReferencesOtherConstant_Evaluates() {
        Dictionary<string, string> consts = new() { ["BASE"] = "100", ["OFFSET"] = "BASE + 4" };
        Assert.That(Analyser.EvaluateVariable("OFFSET", consts), Is.EqualTo(104u));
    }

    [Test]
    public void EvaluateVariable_UndefinedVariable_ThrowsKeyNotFoundException() {
        Assert.Throws<KeyNotFoundException>(() =>
            Analyser.EvaluateVariable("missing", new Dictionary<string, string>()));
    }

    [Test]
    public void EvaluateVariable_WrapAroundUint_HandledCorrectly() {
        // -1 as uint should be 0xFFFFFFFF
        Dictionary<string, string> consts = new() { ["x"] = "0xFFFFFFFF + 1" };
        Assert.That(Analyser.EvaluateVariable("x", consts), Is.EqualTo(0u));
    }

    // ── Debug symbols ────────────────────────────────────────────────────────

    [Test]
    public void DebugSymbols_ContainLineInfoForEachInstruction() {
        (_, _, DebugTable debug) = Analyse("nop\nnop");
        Assert.That(debug.Symbols, Has.Length.EqualTo(2));
        Assert.That(debug.Symbols[0].FilePos, Is.EqualTo(0));
        Assert.That(debug.Symbols[1].FilePos, Is.EqualTo(1));
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Test]
    public void UnknownDirective_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Analyse("#notadirective foo"));
    }

    [Test]
    public void UnknownInstruction_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Analyse("notaninstruction R0, R1, R2, R3"));
    }

    [Test]
    public void Instruction_UndefinedConstantInArg_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Analyse("mov R0, undefinedConst"));
    }

    [Test]
    public void ProcessIncludePath_AbsolutePath_ReturnedAsIs() {
        string abs = "/absolute/path/to/file.asm";
        Assert.That(Analyser.ProcessIncludePath("relative/current.asm", abs), Is.EqualTo(abs));
    }

    [Test]
    public void ProcessIncludePath_RelativePath_CombinedWithCurrentDir() {
        string result = Analyser.ProcessIncludePath("/some/dir/current.asm", "other.asm");
        Assert.That(result, Does.EndWith("other.asm"));
        Assert.That(result, Does.Contain("some"));
        Assert.That(result, Does.Contain("dir"));
    }
}
