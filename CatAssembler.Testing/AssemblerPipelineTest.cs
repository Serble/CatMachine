using System.Text;
using CatAssembler.Analysis;
using CatAssembler.Assembler;
using CatAssembler.Parser;

namespace CatAssembler.Testing;

/// <summary>
/// End-to-end tests that tokenise → analyse → assemble and verify the
/// exact byte output for every major instruction family and data directive.
/// </summary>
[TestFixture]
public class AssemblerPipelineTest {

    /// <summary>Assembles <paramref name="source"/> and returns the raw bytes.</summary>
    private static byte[] Assemble(string source) {
        Token[] tokens = new Tokeniser("test.asm", source).Tokenise();
        (IOutputSegment[] segments, Dictionary<string, string> constants, _) = new Analyser(tokens).Analyse();
        Assembler.Assembler assembler = new(segments, constants);
        using MemoryStream ms = new();
        assembler.WriteTo(ms);
        return ms.ToArray();
    }

    // ── Zero-arg instructions ─────────────────────────────────────────────────

    [Test]
    public void Nop_ProducesSingleByte0x4D() {
        byte[] bytes = Assemble("nop");
        Assert.That(bytes, Is.EqualTo(new byte[] { 0x4D }));
    }

    [Test]
    public void Ret_ProducesSingleByte0x40() {
        byte[] bytes = Assemble("ret");
        Assert.That(bytes, Is.EqualTo(new byte[] { 0x40 }));
    }

    [Test]
    public void Di_ProducesSingleByte0x45() =>
        Assert.That(Assemble("di"), Is.EqualTo(new byte[] { 0x45 }));

    [Test]
    public void Ei_ProducesSingleByte0x46() =>
        Assert.That(Assemble("ei"), Is.EqualTo(new byte[] { 0x46 }));

    [Test]
    public void Iret_ProducesSingleByte0x52() =>
        Assert.That(Assemble("iret"), Is.EqualTo(new byte[] { 0x52 }));

    [Test]
    public void Syscall_ProducesSingleByte0x59() =>
        Assert.That(Assemble("syscall"), Is.EqualTo(new byte[] { 0x59 }));

    // ── MOV (register ↔ register) ────────────────────────────────────────────

    [Test]
    public void Mov_RegReg_OpCode0x00() {
        // mov R0, R1 → [0x00, R0=0x00, R1=0x01]
        Assert.That(Assemble("mov R0, R1"), Is.EqualTo(new byte[] { 0x00, 0x00, 0x01 }));
    }

    [Test]
    public void Mov_RegImm32_OpCode0x01() {
        // mov R0, 1 → [0x01, R0=0x00, 1,0,0,0]
        Assert.That(Assemble("mov R0, 1"),
            Is.EqualTo(new byte[] { 0x01, 0x00, 0x01, 0x00, 0x00, 0x00 }));
    }

    [Test]
    public void Mov_RegRegPointer_OpCode0x02() {
        // mov R0, @R1 → [0x02, R0=0x00, R1=0x01]
        Assert.That(Assemble("mov R0, @R1"), Is.EqualTo(new byte[] { 0x02, 0x00, 0x01 }));
    }

    [Test]
    public void Mov_RegImmPointer_OpCode0x03() {
        // mov R0, @0x10 → [0x03, R0=0x00, 0x10,0,0,0]
        Assert.That(Assemble("mov R0, @0x10"),
            Is.EqualTo(new byte[] { 0x03, 0x00, 0x10, 0x00, 0x00, 0x00 }));
    }

    [Test]
    public void Mov_RegPointerReg_OpCode0x04() {
        // mov @R0, R1 → [0x04, R0=0x00, R1=0x01]
        Assert.That(Assemble("mov @R0, R1"), Is.EqualTo(new byte[] { 0x04, 0x00, 0x01 }));
    }

    [Test]
    public void Mov_ImmPointerReg_OpCode0x06() {
        // mov @0x10, R0 → [0x06, 0x10,0,0,0, R0=0x00]
        Assert.That(Assemble("mov @0x10, R0"),
            Is.EqualTo(new byte[] { 0x06, 0x10, 0x00, 0x00, 0x00, 0x00 }));
    }

    // ── Arithmetic ───────────────────────────────────────────────────────────

    [Test]
    public void Add_RegReg_OpCode0x14() {
        Assert.That(Assemble("add R0, R1"), Is.EqualTo(new byte[] { 0x14, 0x00, 0x01 }));
    }

    [Test]
    public void Add_RegImm_OpCode0x15() {
        Assert.That(Assemble("add R0, 10"),
            Is.EqualTo(new byte[] { 0x15, 0x00, 10, 0, 0, 0 }));
    }

    [Test]
    public void Sub_RegReg_OpCode0x16() {
        Assert.That(Assemble("sub R0, R1"), Is.EqualTo(new byte[] { 0x16, 0x00, 0x01 }));
    }

    [Test]
    public void Sub_RegImm_OpCode0x17() {
        Assert.That(Assemble("sub R0, 5"),
            Is.EqualTo(new byte[] { 0x17, 0x00, 5, 0, 0, 0 }));
    }

    [Test]
    public void Umul_RegReg_OpCode0x18() {
        Assert.That(Assemble("umul R0, R1"), Is.EqualTo(new byte[] { 0x18, 0x00, 0x01 }));
    }

    [Test]
    public void Imul_RegReg_OpCode0x1A() {
        Assert.That(Assemble("imul R0, R1"), Is.EqualTo(new byte[] { 0x1A, 0x00, 0x01 }));
    }

    // ── Bitwise ──────────────────────────────────────────────────────────────

    [Test]
    public void Or_RegReg_OpCode0x29() {
        Assert.That(Assemble("or R0, R1"), Is.EqualTo(new byte[] { 0x29, 0x00, 0x01 }));
    }

    [Test]
    public void And_RegImm_OpCode0x2C() {
        Assert.That(Assemble("and R0, 0xFF"),
            Is.EqualTo(new byte[] { 0x2C, 0x00, 0xFF, 0x00, 0x00, 0x00 }));
    }

    [Test]
    public void Xor_RegReg_OpCode0x2D() {
        Assert.That(Assemble("xor R0, R1"), Is.EqualTo(new byte[] { 0x2D, 0x00, 0x01 }));
    }

    [Test]
    public void Not_Reg_OpCode0x2F() {
        Assert.That(Assemble("not R0"), Is.EqualTo(new byte[] { 0x2F, 0x00 }));
    }

    [Test]
    public void Shl_RegImm_OpCode0x4F() {
        Assert.That(Assemble("shl R0, 2"),
            Is.EqualTo(new byte[] { 0x4F, 0x00, 2, 0, 0, 0 }));
    }

    [Test]
    public void Shr_RegReg_OpCode0x50() {
        Assert.That(Assemble("shr R0, R1"), Is.EqualTo(new byte[] { 0x50, 0x00, 0x01 }));
    }

    // ── Push / Pop ────────────────────────────────────────────────────────────

    [Test]
    public void Push_Reg_OpCode0x20() {
        Assert.That(Assemble("push R0"), Is.EqualTo(new byte[] { 0x20, 0x00 }));
    }

    [Test]
    public void Push_Imm32_OpCode0x21() {
        Assert.That(Assemble("push 42"),
            Is.EqualTo(new byte[] { 0x21, 42, 0, 0, 0 }));
    }

    [Test]
    public void Push16_Imm16_OpCode0x23() {
        Assert.That(Assemble("push16 0x0100"),
            Is.EqualTo(new byte[] { 0x23, 0x00, 0x01 }));
    }

    [Test]
    public void Push8_Imm8_OpCode0x25() {
        Assert.That(Assemble("push8 7"),
            Is.EqualTo(new byte[] { 0x25, 7 }));
    }

    [Test]
    public void Pop_Reg_OpCode0x26() {
        Assert.That(Assemble("pop R0"), Is.EqualTo(new byte[] { 0x26, 0x00 }));
    }

    [Test]
    public void Pop16_Reg_OpCode0x27() {
        Assert.That(Assemble("pop16 R0"), Is.EqualTo(new byte[] { 0x27, 0x00 }));
    }

    [Test]
    public void Pop8_Reg_OpCode0x28() {
        Assert.That(Assemble("pop8 R0"), Is.EqualTo(new byte[] { 0x28, 0x00 }));
    }

    // ── Compare ──────────────────────────────────────────────────────────────

    [Test]
    public void Cmp_RegReg_OpCode0x31() {
        Assert.That(Assemble("cmp R0, R1"), Is.EqualTo(new byte[] { 0x31, 0x00, 0x01 }));
    }

    [Test]
    public void Cmp_ImmImm_OpCode0x34() {
        Assert.That(Assemble("cmp 1, 2"),
            Is.EqualTo(new byte[] { 0x34, 1, 0, 0, 0, 2, 0, 0, 0 }));
    }

    // ── Jump-style instructions (JumpStyleInstruction custom handler) ─────────

    [Test]
    public void Jmp_ImmediateOnly_RegisterSlotIsFF() {
        // jmp 100 → [0x30, 0xFF, 100,0,0,0]
        byte[] bytes = Assemble("jmp 100");
        Assert.That(bytes[0], Is.EqualTo(0x30));
        Assert.That(bytes[1], Is.EqualTo(0xFF));
        Assert.That(bytes[2], Is.EqualTo(100));
    }

    [Test]
    public void Jmp_RegisterOnly_ImmediateSlotIsZero() {
        // jmp R0 → [0x30, R0=0x00, 0,0,0,0]
        byte[] bytes = Assemble("jmp R0");
        Assert.That(bytes[0], Is.EqualTo(0x30));
        Assert.That(bytes[1], Is.EqualTo(0x00));
        Assert.That(BitConverter.ToUInt32(bytes, 2), Is.EqualTo(0u));
    }

    [Test]
    public void Jmp_RegisterAndImmediate_BothEncoded() {
        // jmp R1, 200 → [0x30, R1=0x01, 200,0,0,0]
        byte[] bytes = Assemble("jmp R1, 200");
        Assert.That(bytes[0], Is.EqualTo(0x30));
        Assert.That(bytes[1], Is.EqualTo(0x01));
        Assert.That(BitConverter.ToUInt32(bytes, 2), Is.EqualTo(200u));
    }

    [Test]
    public void Je_IsAliasForJz() {
        byte[] jz = Assemble("jz R0, 0");
        byte[] je = Assemble("je R0, 0");
        Assert.That(je, Is.EqualTo(jz));
    }

    [Test]
    public void Jne_IsAliasForJnz() {
        byte[] jnz = Assemble("jnz R0, 0");
        byte[] jne = Assemble("jne R0, 0");
        Assert.That(jne, Is.EqualTo(jnz));
    }

    [Test]
    public void Call_EncodesSameSizeAsJmp() {
        // call has opcode 0x3F, same 6-byte structure as jmp
        byte[] bytes = Assemble("call R0, 0");
        Assert.That(bytes, Has.Length.EqualTo(6));
        Assert.That(bytes[0], Is.EqualTo(0x3F));
    }

    // ── Jump to label ────────────────────────────────────────────────────────

    [Test]
    public void Jmp_ToLabel_ResolvesLabelAddressAtAssemblyTime() {
        // nop is 1 byte (position 0), target label is at position 1
        // jmp target → [0x30, 0xFF, 1,0,0,0]
        byte[] bytes = Assemble("""
            nop
            target:
            jmp target
            """);
        Assert.That(bytes[0], Is.EqualTo(0x4D));      // nop
        Assert.That(bytes[1], Is.EqualTo(0x30));       // jmp opcode
        Assert.That(bytes[2], Is.EqualTo(0xFF));       // no register
        Assert.That(BitConverter.ToUInt32(bytes, 3), Is.EqualTo(1u)); // label address = 1
    }

    // ── Interrupt ────────────────────────────────────────────────────────────

    [Test]
    public void Int_Imm8_OpCode0x1F() {
        Assert.That(Assemble("int 0x01"), Is.EqualTo(new byte[] { 0x1F, 0x01 }));
    }

    [Test]
    public void Int_Reg_OpCode0x1E() {
        Assert.That(Assemble("int R0"), Is.EqualTo(new byte[] { 0x1E, 0x00 }));
    }

    // ── Data directives ───────────────────────────────────────────────────────

    [Test]
    public void D8_MultipleBytes_PackedCorrectly() {
        byte[] bytes = Assemble("d8 1, 2, 3");
        Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
    }

    [Test]
    public void D16_TwoBytes_LittleEndian() {
        byte[] bytes = Assemble("d16 0x0102");
        Assert.That(bytes, Is.EqualTo(new byte[] { 0x02, 0x01 }));
    }

    [Test]
    public void D32_FourBytes_LittleEndian() {
        byte[] bytes = Assemble("d32 0x12345678");
        Assert.That(bytes, Is.EqualTo(new byte[] { 0x78, 0x56, 0x34, 0x12 }));
    }

    [Test]
    public void D8_ConstantExpression_Resolves() {
        byte[] bytes = Assemble("""
            #define V, 200
            d8 V
            """);
        Assert.That(bytes, Is.EqualTo(new byte[] { 200 }));
    }

    [Test]
    public void Res8_ReservesZeroedBytes() {
        byte[] bytes = Assemble("res8 4");
        Assert.That(bytes, Is.EqualTo(new byte[] { 0, 0, 0, 0 }));
    }

    [Test]
    public void Res16_ReservesCorrectByteCount() {
        // res16 3 → 3 × 2 = 6 zero bytes
        byte[] bytes = Assemble("res16 3");
        Assert.That(bytes, Has.Length.EqualTo(6));
        Assert.That(bytes, Is.All.EqualTo((byte)0));
    }

    [Test]
    public void Res32_ReservesCorrectByteCount() {
        byte[] bytes = Assemble("res32 2");
        Assert.That(bytes, Has.Length.EqualTo(8));
    }

    [Test]
    public void Dstr_EmitsUtf8Bytes() {
        byte[] bytes = Assemble("""dstr "hello" """);
        Assert.That(bytes, Is.EqualTo(Encoding.UTF8.GetBytes("hello")));
    }

    [Test]
    public void Dstr_EscapeNewline_EmittedAsNewlineByte() {
        byte[] bytes = Assemble("""dstr "a\nb" """);
        Assert.That(bytes, Is.EqualTo(new byte[] { (byte)'a', (byte)'\n', (byte)'b' }));
    }

    // ── #define constant used in instruction arg ──────────────────────────────

    [Test]
    public void Define_UsedAsInstructionArg_ResolvesCorrectly() {
        byte[] bytes = Assemble("""
            #define PORT, 5
            out PORT, R0
            """);
        // out i, r → opcode 0x4B, imm32=5, R0=0x00
        Assert.That(bytes[0], Is.EqualTo(0x4B));
        Assert.That(BitConverter.ToUInt32(bytes, 1), Is.EqualTo(5u));
        Assert.That(bytes[5], Is.EqualTo(0x00));
    }

    // ── Macro expansion producing correct bytes ───────────────────────────────

    [Test]
    public void Macro_ExpandedBytesMatchInlinedEquivalent() {
        byte[] withMacro = Assemble("""
            #macro push2, 2
            push $1
            push $2
            #endmacro
            push2 R0, R1
            """);
        byte[] inline = Assemble("""
            push R0
            push R1
            """);
        Assert.That(withMacro, Is.EqualTo(inline));
    }

    // ── Multiple instructions back-to-back ────────────────────────────────────

    [Test]
    public void MultipleInstructions_ConcatenatedInOrder() {
        byte[] bytes = Assemble("""
            nop
            ret
            nop
            """);
        Assert.That(bytes, Is.EqualTo(new byte[] { 0x4D, 0x40, 0x4D }));
    }

    // ── Mov32 alias ───────────────────────────────────────────────────────────

    [Test]
    public void Mov32_IsSameAsMov() {
        byte[] mov = Assemble("mov R0, R1");
        byte[] mov32 = Assemble("mov32 R0, R1");
        Assert.That(mov32, Is.EqualTo(mov));
    }

    // ── In / Out I/O instructions ─────────────────────────────────────────────

    [Test]
    public void In_RegReg_OpCode0x47() {
        Assert.That(Assemble("in R0, R1"), Is.EqualTo(new byte[] { 0x47, 0x00, 0x01 }));
    }

    [Test]
    public void Out_RegReg_OpCode0x49() {
        Assert.That(Assemble("out R0, R1"), Is.EqualTo(new byte[] { 0x49, 0x00, 0x01 }));
    }

    // ── Interrupt table / kernel stack pointer ────────────────────────────────

    [Test]
    public void Setit_Imm_OpCode0x54() {
        Assert.That(Assemble("setit 0x1000"),
            Is.EqualTo(new byte[] { 0x54, 0x00, 0x10, 0x00, 0x00 }));
    }

    [Test]
    public void Getit_Reg_OpCode0x55() {
        Assert.That(Assemble("getit R0"), Is.EqualTo(new byte[] { 0x55, 0x00 }));
    }

    [Test]
    public void Setksp_Reg_OpCode0x56() {
        Assert.That(Assemble("setksp R0"), Is.EqualTo(new byte[] { 0x56, 0x00 }));
    }

    [Test]
    public void Getksp_Reg_OpCode0x58() {
        Assert.That(Assemble("getksp R0"), Is.EqualTo(new byte[] { 0x58, 0x00 }));
    }
}
