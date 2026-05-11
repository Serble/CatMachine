using CatAssembler.Analysis;
using CatAssembler.Assembler;

namespace CatAssembler;

public static class Spec {
    public static readonly InstructionSpec[] Instructions = [
        // MOV32
        new(["mov", "mov32"], 0x00, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)), // r, r
        new(["mov", "mov32"], 0x01, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)), // r, i
        new(["mov", "mov32"], 0x02, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, true)), // r, rp
        new(["mov", "mov32"], 0x03, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, true)), // r, ip
        new(["mov", "mov32"], 0x04, (InstructionArgTypes.Register, true), (InstructionArgTypes.Register, false)), // rp, r
        new(["mov", "mov32"], 0x05, (InstructionArgTypes.Register, true), (InstructionArgTypes.Immediate32, false)), // rp, i
        new(["mov", "mov32"], 0x06, (InstructionArgTypes.Immediate32, true), (InstructionArgTypes.Register, false)), // ip, r
        new(["mov", "mov32"], 0x07, (InstructionArgTypes.Immediate32, true), (InstructionArgTypes.Immediate32, false)), // ip, i
        
        // MOV16
        new(["mov16"], 0x08, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, true)),
        new(["mov16"], 0x09, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, true)), // r, ip
        new(["mov16"], 0x0a, (InstructionArgTypes.Register, true), (InstructionArgTypes.Register, false)), // rp, r
        new(["mov16"], 0x0b, (InstructionArgTypes.Register, true), (InstructionArgTypes.Immediate16, false)), // rp, i16
        new(["mov16"], 0x0c, (InstructionArgTypes.Immediate32, true), (InstructionArgTypes.Register, false)), // ip, r
        new(["mov16"], 0x0d, (InstructionArgTypes.Immediate32, true), (InstructionArgTypes.Immediate16, false)), // ip, i16

        // MOV8
        new(["mov8"], 0x0e, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, true)), // r, rp
        new(["mov8"], 0x0f, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, true)), // r, ip
        new(["mov8"], 0x10, (InstructionArgTypes.Register, true), (InstructionArgTypes.Register, false)), // rp, r
        new(["mov8"], 0x11, (InstructionArgTypes.Register, true), (InstructionArgTypes.Immediate8, false)), // rp, i8
        new(["mov8"], 0x12, (InstructionArgTypes.Immediate32, true), (InstructionArgTypes.Register, false)), // ip, r
        new(["mov8"], 0x13, (InstructionArgTypes.Immediate32, true), (InstructionArgTypes.Immediate8, false)), // ip, i8

        // ADD
        new(["add"], 0x14, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)), // r, r
        new(["add"], 0x15, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)), // r, i

        // SUB
        new(["sub"], 0x16, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)), // r, r
        new(["sub"], 0x17, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)), // r, i

        // UMUL
        new(["umul"], 0x18, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)), // r, r
        new(["umul"], 0x19, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)), // r, i

        // IMUL
        new(["imul"], 0x1a, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)), // r, r
        new(["imul"], 0x1b, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)), // r, i

        // UDIV (special notes in CSV)
        new(["udiv"], 0x1c, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)), // r, r
        // IDIV
        new(["idiv"], 0x1d, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)), // r, r

        // INT
        new(["int"], 0x1e, (InstructionArgTypes.Register, false)), // rb
        new(["int"], 0x1f, (InstructionArgTypes.Immediate8, false)), // i8

        // PUSH
        new(["push", "push32"], 0x20, (InstructionArgTypes.Register, false)), // r
        new(["push", "push32"], 0x21, (InstructionArgTypes.Immediate32, false)), // i
        new(["push16"], 0x22, (InstructionArgTypes.Register, false)), // r
        new(["push16"], 0x23, (InstructionArgTypes.Immediate16, false)), // i16
        new(["push8"], 0x24, (InstructionArgTypes.Register, false)), // r
        new(["push8"], 0x25, (InstructionArgTypes.Immediate8, false)), // i8

        // POP
        new(["pop", "pop32"], 0x26, (InstructionArgTypes.Register, false)),
        new(["pop16"], 0x27, (InstructionArgTypes.Register, false)),
        new(["pop8"], 0x28, (InstructionArgTypes.Register, false)),

        // OR
        new(["or"], 0x29, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)),
        new(["or"], 0x2a, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),

        // AND
        new(["and"], 0x2b, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)),
        new(["and"], 0x2c, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),

        // XOR
        new(["xor"], 0x2d, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)),
        new(["xor"], 0x2e, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),

        // NOT
        new(["not"], 0x2f, (InstructionArgTypes.Register, false)),

        // JMP
        new(["jmp"], 0x30, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),

        // CMP
        new(["cmp"], 0x31, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)),
        new(["cmp"], 0x32, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),
        new(["cmp"], 0x33, (InstructionArgTypes.Immediate32, false), (InstructionArgTypes.Register, false)),
        new(["cmp"], 0x34, (InstructionArgTypes.Immediate32, false), (InstructionArgTypes.Immediate32, false)),

        // JZ/JE
        new(["jz", "je"], 0x35, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),
        // JNZ/JNE
        new(["jnz", "jne"], 0x36, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),

        // JUL, JULE, JUG, JUGE
        new(["jul"], 0x37, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),
        new(["jule"], 0x38, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),
        new(["jug"], 0x39, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),
        new(["juge"], 0x3a, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),

        // JIL, JILE, JIG, JIGE
        new(["jil"], 0x3b, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),
        new(["jile"], 0x3c, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),
        new(["jig"], 0x3d, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),
        new(["jige"], 0x3e, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),

        // CALL
        new(["call"], 0x3f, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),

        // RET
        new(["ret"], 0x40),

        // CPY
        new(["cpy"], 0x41, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)),
        new(["cpy"], 0x42, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),
        new(["cpy"], 0x43, (InstructionArgTypes.Immediate32, false), (InstructionArgTypes.Register, false)),
        new(["cpy"], 0x44, (InstructionArgTypes.Immediate32, false), (InstructionArgTypes.Immediate32, false)),

        // DI, EI
        new(["di"], 0x45),
        new(["ei"], 0x46),

        // IN
        new(["in"], 0x47, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)),
        new(["in"], 0x48, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),
        
        // OUT
        new(["out"], 0x49, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)),     // port, data
        new(["out"], 0x4a, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),  // port, i
        new(["out"], 0x4b, (InstructionArgTypes.Immediate32, false), (InstructionArgTypes.Register, false)),  // i, data
        new(["out"], 0x4c, (InstructionArgTypes.Immediate32, false), (InstructionArgTypes.Immediate32, false)), // i, i

        // NOP
        new(["nop"], 0x4d),
        
        // SHL, SHR
        new(["shl"], 0x4e, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)),
        new(["shl"], 0x4f, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),
        new(["shr"], 0x50, (InstructionArgTypes.Register, false), (InstructionArgTypes.Register, false)),
        new(["shr"], 0x51, (InstructionArgTypes.Register, false), (InstructionArgTypes.Immediate32, false)),
        
        // IRET
        new(["iret"], 0x52),

        // IT
        new(["setit"], 0x53, (InstructionArgTypes.Register, false)),
        new(["setit"], 0x54, (InstructionArgTypes.Immediate32, false)),
        new(["getit"], 0x55, (InstructionArgTypes.Register, false)),

        // KSP
        new(["setksp"], 0x56, (InstructionArgTypes.Register, false)),
        new(["setksp"], 0x57, (InstructionArgTypes.Immediate32, false)),
        new(["getksp"], 0x58, (InstructionArgTypes.Register, false)),

        // SYSCALL
        new(["syscall"], 0x59),
        
        // TIMING
        new(["uptms"], 0x5a),
        new(["uptns"], 0x5b),
    ];

    public static readonly (string[] Mneumonics, IOutputSegment Segment)[] CustomInstructions = [
        // Reserve directives
        (["res8"], new ReserveInstruction(1)),
        (["res16"], new ReserveInstruction(2)),
        (["res32"], new ReserveInstruction(4)),
        
        // Define directives
        (["d8"], new DefineInstruction(1)),
        (["d16"], new DefineInstruction(2)),
        (["d32"], new DefineInstruction(4)),
        (["dfile"], new DirectFileInstruction()),
        (["dstr"], new DirectStringInstruction()),
        
        // Jump style directives
        (["jmp"], new JumpStyleInstruction(0x30)),
        (["jz", "je"], new JumpStyleInstruction(0x35)),
        (["jnz", "jne"], new JumpStyleInstruction(0x36)),
        (["jul"], new JumpStyleInstruction(0x37)),
        (["jule"], new JumpStyleInstruction(0x38)),
        (["jug"], new JumpStyleInstruction(0x39)),
        (["juge"], new JumpStyleInstruction(0x3a)),
        (["jil"], new JumpStyleInstruction(0x3b)),
        (["jile"], new JumpStyleInstruction(0x3c)),
        (["jig"], new JumpStyleInstruction(0x3d)),
        (["jige"], new JumpStyleInstruction(0x3e)),
        (["call"], new JumpStyleInstruction(0x3f))
    ];
}

// mem is whether the argument is a memory address (i.e. @R0 vs R0)
public record InstructionSpec(string[] Mneumonics, byte Id, params (IInstructionArgType type, bool mem)[] ArgTypes) {
    
    public InstructionSpec(string mneumonic, byte Id, params (IInstructionArgType type, bool mem)[] ArgTypes)
        : this([mneumonic], Id, ArgTypes) { }
}
