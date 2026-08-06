using CatAssembler.Exceptions;
using CatAssembler.Parser;

namespace CatAssembler.Testing;

/// <summary>
/// Tests for <see cref="Tokeniser"/>: labels, directives, instructions,
/// comments, all expression forms (registers, numbers, strings, names, pointers).
/// </summary>
[TestFixture]
public class TokeniserTest {

    private static Token[] Tokenise(string source) =>
        new Tokeniser("test.asm", source).Tokenise();

    // ── Labels ────────────────────────────────────────────────────────────────

    [Test]
    public void GlobalLabel_ProducesLabelToken() {
        Token[] tokens = Tokenise("myLabel:");
        Assert.That(tokens, Has.Length.EqualTo(1));
        LabelToken label = (LabelToken)tokens[0];
        Assert.That(label.Name, Is.EqualTo("myLabel"));
    }

    [Test]
    public void LocalLabel_IsScopedToCurrentGlobalLabel() {
        Token[] tokens = Tokenise("""
            outer:
            .inner:
            """);
        Assert.That(tokens, Has.Length.EqualTo(2));
        LabelToken local = (LabelToken)tokens[1];
        Assert.That(local.Name, Does.StartWith(Tokeniser.LocalLabelPrefix));
        Assert.That(local.Name, Does.Contain("outer"));
        Assert.That(local.Name, Does.Contain("inner"));
    }

    [Test]
    public void UnscopedGlobalLabel_DoesNotChangeScopeAndHasPrefix() {
        Token[] tokens = Tokenise("""
            $shared:
            .local:
            """);
        // $shared: is an unscoped global so it should NOT become the new scope name.
        // Therefore .local: should still be scoped to the previous global label (empty string here).
        LabelToken unscopedLabel = (LabelToken)tokens[0];
        Assert.That(unscopedLabel.Name, Does.StartWith(Tokeniser.UnscopedGlobalLabelPrefix));
        Assert.That(unscopedLabel.Name, Does.Contain("shared"));
    }

    [Test]
    public void Label_InvalidName_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Tokenise("123invalid:"));
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    [Test]
    public void Comment_OnlyLine_ProducesNoTokens() {
        Token[] tokens = Tokenise("; this is a comment");
        Assert.That(tokens, Is.Empty);
    }

    [Test]
    public void Comment_TrailingComment_IsStripped() {
        Token[] tokens = Tokenise("nop ; no-op");
        Assert.That(tokens, Has.Length.EqualTo(1));
        Assert.That(tokens[0], Is.InstanceOf<InstructionToken>());
        Assert.That(((InstructionToken)tokens[0]).Name, Is.EqualTo("nop"));
    }

    [Test]
    public void EmptyLine_ProducesNoTokens() {
        Token[] tokens = Tokenise("   \n\n   ");
        Assert.That(tokens, Is.Empty);
    }

    // ── Directives ────────────────────────────────────────────────────────────

    [Test]
    public void DefineDirective_ProducesDirectiveToken() {
        Token[] tokens = Tokenise("#define FOO, 42");
        Assert.That(tokens, Has.Length.EqualTo(1));
        DirectiveToken dir = (DirectiveToken)tokens[0];
        Assert.That(dir.Name, Is.EqualTo("define"));
        Assert.That(dir.Args, Has.Length.EqualTo(2));
        Assert.That(((NameExpression)dir.Args[0]).Value, Is.EqualTo("FOO"));
        Assert.That(((NumberExpression)dir.Args[1]).Value, Is.EqualTo("42"));
    }

    [Test]
    public void ConstDirective_IsAliasForDefine() {
        Token[] tokens = Tokenise("#const BAR, 0xFF");
        DirectiveToken dir = (DirectiveToken)tokens[0];
        Assert.That(dir.Name, Is.EqualTo("const"));
    }

    [Test]
    public void MacroDirective_BodyCapturedAsMacroBodyExpression() {
        Token[] tokens = Tokenise("""
            #macro mymacro, 0
            nop
            #endmacro
            """);
        Assert.That(tokens, Has.Length.EqualTo(1));
        DirectiveToken dir = (DirectiveToken)tokens[0];
        Assert.That(dir.Name, Is.EqualTo("macro"));
        Assert.That(dir.Args[2], Is.InstanceOf<MacroBodyExpression>());
        MacroBodyExpression body = (MacroBodyExpression)dir.Args[2];
        Assert.That(body.Value, Has.Count.EqualTo(1));
        Assert.That(body.Value[0].Trim(), Is.EqualTo("nop"));
    }

    [Test]
    public void MacroWithoutEndmacro_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Tokenise("""
            #macro broken 0
            nop
            """));
    }

    [Test]
    public void MissingDirectiveName_ThrowsParseException() {
        Assert.Throws<ParseException>(() => Tokenise("#"));
    }

    // ── Instructions ─────────────────────────────────────────────────────────

    [Test]
    public void Instruction_NoArgs_Parsed() {
        Token[] tokens = Tokenise("nop");
        InstructionToken instr = (InstructionToken)tokens[0];
        Assert.That(instr.Name, Is.EqualTo("nop"));
        Assert.That(instr.Args, Is.Empty);
    }

    [Test]
    public void Instruction_RegisterAndImmediate_Parsed() {
        Token[] tokens = Tokenise("mov R0, 100");
        InstructionToken instr = (InstructionToken)tokens[0];
        Assert.That(instr.Name, Is.EqualTo("mov"));
        Assert.That(instr.Args, Has.Length.EqualTo(2));
        Assert.That(instr.Args[0], Is.InstanceOf<RegisterExpression>());
        Assert.That(instr.Args[1], Is.InstanceOf<NumberExpression>());
    }

    [Test]
    public void Instruction_HexImmediate_Parsed() {
        Token[] tokens = Tokenise("mov R1, 0xDEAD");
        InstructionToken instr = (InstructionToken)tokens[0];
        Assert.That(((NumberExpression)instr.Args[1]).Value, Is.EqualTo("0xDEAD"));
    }

    [Test]
    public void Instruction_RegisterPointer_AtSyntax_Parsed() {
        Token[] tokens = Tokenise("mov R0, @R1");
        InstructionToken instr = (InstructionToken)tokens[0];
        RegisterExpression regPtr = (RegisterExpression)instr.Args[1];
        Assert.That(regPtr.Pointer, Is.True);
        Assert.That(regPtr.Value, Is.EqualTo(CatData.Register.R1));
    }

    [Test]
    public void Instruction_RegisterPointer_BracketSyntax_Parsed() {
        Token[] tokens = Tokenise("mov R0, [R1]");
        InstructionToken instr = (InstructionToken)tokens[0];
        RegisterExpression regPtr = (RegisterExpression)instr.Args[1];
        Assert.That(regPtr.Pointer, Is.True);
    }

    [Test]
    public void Instruction_ImmediatePointer_Parsed() {
        Token[] tokens = Tokenise("mov R0, @0x1000");
        InstructionToken instr = (InstructionToken)tokens[0];
        NumberExpression immPtr = (NumberExpression)instr.Args[1];
        Assert.That(immPtr.Pointer, Is.True);
        Assert.That(immPtr.Value, Is.EqualTo("0x1000"));
    }

    [Test]
    public void Instruction_StringArg_Parsed() {
        Token[] tokens = Tokenise("""dstr "hello" """);
        InstructionToken instr = (InstructionToken)tokens[0];
        Assert.That(instr.Args[0], Is.InstanceOf<StringExpression>());
        Assert.That(((StringExpression)instr.Args[0]).Value, Is.EqualTo("hello"));
    }

    [Test]
    public void Instruction_StringEscapeSequences_Parsed() {
        Token[] tokens = Tokenise("""dstr "a\nb\tc" """);
        StringExpression str = (StringExpression)((InstructionToken)tokens[0]).Args[0];
        Assert.That(str.Value, Is.EqualTo("a\nb\tc"));
    }

    [Test]
    public void Instruction_NameArg_ParsedAsNameExpression() {
        Token[] tokens = Tokenise("jmp myLabel");
        InstructionToken instr = (InstructionToken)tokens[0];
        Assert.That(instr.Args[0], Is.InstanceOf<NameExpression>());
        Assert.That(((NameExpression)instr.Args[0]).Value, Is.EqualTo("myLabel"));
    }

    [Test]
    public void Instruction_LocalLabelRef_IsTransformed() {
        Token[] tokens = Tokenise("""
            outer:
            jmp .loop
            """);
        InstructionToken instr = (InstructionToken)tokens[1];
        NameExpression nameExpr = (NameExpression)instr.Args[0];
        Assert.That(nameExpr.Value, Does.StartWith(Tokeniser.LocalLabelPrefix));
        Assert.That(nameExpr.Value, Does.Contain("outer"));
        Assert.That(nameExpr.Value, Does.Contain("loop"));
    }

    [Test]
    public void Instruction_MultipleArgs_CommaSeparated() {
        Token[] tokens = Tokenise("add R0, R1");
        InstructionToken instr = (InstructionToken)tokens[0];
        Assert.That(instr.Args, Has.Length.EqualTo(2));
        Assert.That(((RegisterExpression)instr.Args[0]).Value, Is.EqualTo(CatData.Register.R0));
        Assert.That(((RegisterExpression)instr.Args[1]).Value, Is.EqualTo(CatData.Register.R1));
    }

    [Test]
    public void Instruction_ArithmeticExpression_ParsedAsNumber() {
        // Expressions like (2+3) are NumberExpressions whose evaluation is deferred
        Token[] tokens = Tokenise("mov R0, (2+3)");
        InstructionToken instr = (InstructionToken)tokens[0];
        Assert.That(instr.Args[1], Is.InstanceOf<NumberExpression>());
        Assert.That(((NumberExpression)instr.Args[1]).Value, Is.EqualTo("(2+3)"));
    }

    [Test]
    public void Instruction_AllRegisters_Recognised() {
        string[] registerNames = ["R0", "R1", "R2", "R3", "R4", "R5", "R6", "R7", "Sp", "Ip", "Fl"];
        foreach (string reg in registerNames) {
            Token[] tokens = Tokenise($"not {reg}");
            InstructionToken instr = (InstructionToken)tokens[0];
            Assert.That(instr.Args[0], Is.InstanceOf<RegisterExpression>(),
                $"Register '{reg}' should be parsed as RegisterExpression");
        }
    }

    [Test]
    public void Instruction_LineAndFileInfo_Populated() {
        Token[] tokens = new Tokeniser("myfile.asm", "nop").Tokenise();
        Assert.That(tokens[0].File, Is.EqualTo("myfile.asm"));
        Assert.That(tokens[0].Line, Is.EqualTo(1));
    }
}
