//===-- CatInstrInfo.td - Target Description for Cat ------*- tablegen -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file describes the Cat instructions in TableGen format.
//
//===----------------------------------------------------------------------===//

//===----------------------------------------------------------------------===//
// Instruction format superclass
//===----------------------------------------------------------------------===//

class CatInst<dag outs, dag ins, string opcodestr, string argstr,
              list<dag> pattern, InstrItinClass itin = NoItinerary>
    : Instruction {
  field bits<32> Inst;
  field bits<32> SoftFail = 0;
  let Size = 4;

  let Namespace = "Cat";
  let OutOperandList = outs;
  let InOperandList = ins;
  let AsmString = opcodestr # "\t" # argstr;
  let Pattern = pattern;
  let Itinerary = itin;

  bits<8> Opcode = 0;
  let Inst{7-0} = Opcode;
}

//===----------------------------------------------------------------------===//
// Operand Definitions
//===----------------------------------------------------------------------===//

def simm32 : Operand<i32>, ImmLeaf<i32, [{return isInt<32>(Imm);}]>;
def uimm32 : Operand<i32>, ImmLeaf<i32, [{return isUInt<32>(Imm);}]>;

def brtarget : Operand<OtherVT> {
  let EncoderMethod = "getBranchTargetOpValue";
  let DecoderMethod = "decodeBranchTarget";
}

def calltarget : Operand<i32> {
  let EncoderMethod = "getCallTargetOpValue";
  let DecoderMethod = "decodeCallTarget";
}

//===----------------------------------------------------------------------===//
// SDNode Definitions
//===----------------------------------------------------------------------===//

def SDT_CatRet : SDTypeProfile<0, 0, []>;

def ret : SDNode<"ISD::RETURN", SDT_CatRet,
                 [SDNPHasChain, SDNPOptInGlue, SDNPVariadic]>;

// Call sequence SDNodes
def SDT_CatCallSeqStart : SDTypeProfile<0, 2, [SDTCisVT<0, i32>, SDTCisVT<1, i32>]>;
def SDT_CatCallSeqEnd : SDTypeProfile<0, 2, [SDTCisVT<0, i32>, SDTCisVT<1, i32>]>;

def callseq_start : SDNode<"ISD::CALLSEQ_START", SDT_CatCallSeqStart,
                           [SDNPHasChain, SDNPOutGlue]>;
def callseq_end : SDNode<"ISD::CALLSEQ_END", SDT_CatCallSeqEnd,
                         [SDNPHasChain, SDNPOptInGlue, SDNPOutGlue]>;

//===----------------------------------------------------------------------===//
// Instruction Classes
//===----------------------------------------------------------------------===//

// Register to Register
class ALU_RR<bits<8> opcode, string opcodestr, SDNode opnode>
    : CatInst<(outs GPR:$rd), (ins GPR:$rs1, GPR:$rs2),
              opcodestr, "$rd, $rs1, $rs2",
              [(set GPR:$rd, (opnode GPR:$rs1, GPR:$rs2))]> {
  let Opcode = opcode;
}

// Register to Immediate
class ALU_RI<bits<8> opcode, string opcodestr, SDNode opnode>
    : CatInst<(outs GPR:$rd), (ins GPR:$rs1, simm32:$imm),
              opcodestr, "$rd, $rs1, $imm",
              [(set GPR:$rd, (opnode GPR:$rs1, simm32:$imm))]> {
  let Opcode = opcode;
}

// Move Instructions
class MOV_RR<bits<8> opcode, string opcodestr>
    : CatInst<(outs GPR:$rd), (ins GPR:$rs),
              opcodestr, "$rd, $rs",
              []> {
  let Opcode = opcode;
}

class MOV_RI<bits<8> opcode, string opcodestr>
    : CatInst<(outs GPR:$rd), (ins simm32:$imm),
              opcodestr, "$rd, $imm",
              [(set GPR:$rd, simm32:$imm)]> {
  let Opcode = opcode;
}

// Load/Store Instructions
class LOAD<bits<8> opcode, string opcodestr, PatFrag loadop>
    : CatInst<(outs GPR:$rd), (ins GPR:$addr),
              opcodestr, "$rd, @$addr",
              [(set GPR:$rd, (loadop GPR:$addr))]> {
  let Opcode = opcode;
  let mayLoad = 1;
}

class STORE<bits<8> opcode, string opcodestr, PatFrag storeop>
    : CatInst<(outs), (ins GPR:$rs, GPR:$addr),
              opcodestr, "@$addr, $rs",
              [(storeop GPR:$rs, GPR:$addr)]> {
  let Opcode = opcode;
  let mayStore = 1;
}

// Branch Instructions
class BRANCH<bits<8> opcode, string opcodestr>
    : CatInst<(outs), (ins GPR:$cond, brtarget:$target),
              opcodestr, "$cond, $target",
              []> {
  let Opcode = opcode;
  let isBranch = 1;
  let isTerminator = 1;
}

class BRANCH_COND<bits<8> opcode, string opcodestr>
    : CatInst<(outs), (ins GPR:$cond, brtarget:$target),
              opcodestr, "$cond, $target",
              []> {
  let Opcode = opcode;
  let isBranch = 1;
  let isTerminator = 1;
}

//===----------------------------------------------------------------------===//
// Instruction Definitions
//===----------------------------------------------------------------------===//

let hasSideEffects = 0, mayLoad = 0, mayStore = 0 in {

// Move instructions (MOV r,r and MOV r,i)
def MOV32_RR : MOV_RR<0x00, "MOV">;
def MOV32_RI : MOV_RI<0x01, "MOV">;

// Arithmetic operations
def ADD_RR : ALU_RR<0x14, "ADD", add>;
def ADD_RI : ALU_RI<0x15, "ADD", add>;
def SUB_RR : ALU_RR<0x16, "SUB", sub>;
def SUB_RI : ALU_RI<0x17, "SUB", sub>;
def UMUL_RR : ALU_RR<0x18, "UMUL", mul>;
def UMUL_RI : ALU_RI<0x19, "UMUL", mul>;
def IMUL_RR : ALU_RR<0x1a, "IMUL", mul>;
def IMUL_RI : ALU_RI<0x1b, "IMUL", mul>;

// Logical operations
def OR_RR : ALU_RR<0x29, "OR", or>;
def OR_RI : ALU_RI<0x2a, "OR", or>;
def AND_RR : ALU_RR<0x2b, "AND", and>;
def AND_RI : ALU_RI<0x2c, "AND", and>;
def XOR_RR : ALU_RR<0x2d, "XOR", xor>;
def XOR_RI : ALU_RI<0x2e, "XOR", xor>;

def NOT : CatInst<(outs GPR:$rd), (ins GPR:$rs),
                  "NOT", "$rd, $rs",
                  [(set GPR:$rd, (not GPR:$rs))]> {
  let Opcode = 0x2f;
}

} // hasSideEffects = 0, mayLoad = 0, mayStore = 0

// Load/Store operations
def MOV32_LOAD : LOAD<0x02, "MOV", load>;
def MOV32_STORE : STORE<0x04, "MOV", store>;
def MOV16_LOAD : LOAD<0x08, "MOV16", zextloadi16>;
def MOV16_STORE : STORE<0x0a, "MOV16", truncstorei16>;
def MOV8_LOAD : LOAD<0x0e, "MOV8", zextloadi8>;
def MOV8_STORE : STORE<0x10, "MOV8", truncstorei8>;

// Stack operations
let Defs = [SP], Uses = [SP] in {
def PUSH32 : CatInst<(outs), (ins GPR:$rs),
                     "PUSH", "$rs",
                     []> {
  let Opcode = 0x20;
  let mayStore = 1;
}

def POP32 : CatInst<(outs GPR:$rd), (ins),
                    "POP", "$rd",
                    []> {
  let Opcode = 0x26;
  let mayLoad = 1;
}
}

// Compare instruction
let Defs = [FL] in {
def CMP_RR : CatInst<(outs), (ins GPR:$rs1, GPR:$rs2),
                     "CMP", "$rs1, $rs2",
                     []> {
  let Opcode = 0x31;
}

def CMP_RI : CatInst<(outs), (ins GPR:$rs, simm32:$imm),
                     "CMP", "$rs, $imm",
                     []> {
  let Opcode = 0x32;
}
}

// Jump instructions
let isBarrier = 1, isTerminator = 1 in {
def JMP : CatInst<(outs), (ins brtarget:$target),
                  "JMP", "0xFF, $target",
                  [(br bb:$target)]> {
  let Opcode = 0x30;
  let isBranch = 1;
}
}

// Conditional branches
let isBranch = 1, isTerminator = 1, Uses = [FL] in {
def JZ  : BRANCH_COND<0x35, "JZ">;   // Jump if zero
def JNZ : BRANCH_COND<0x36, "JNZ">;  // Jump if not zero
def JUL : BRANCH_COND<0x37, "JUL">;  // Jump if unsigned less
def JULE : BRANCH_COND<0x38, "JULE">; // Jump if unsigned less or equal
def JUG : BRANCH_COND<0x39, "JUG">;  // Jump if unsigned greater
def JUGE : BRANCH_COND<0x3a, "JUGE">; // Jump if unsigned greater or equal
def JIL : BRANCH_COND<0x3b, "JIL">;  // Jump if signed less
def JILE : BRANCH_COND<0x3c, "JILE">; // Jump if signed less or equal
def JIG : BRANCH_COND<0x3d, "JIG">;  // Jump if signed greater
def JIGE : BRANCH_COND<0x3e, "JIGE">; // Jump if signed greater or equal
}

// Call and Return
let isCall = 1, Defs = [R0, R1, R2, R3], Uses = [SP] in {
def CALL : CatInst<(outs), (ins calltarget:$target),
                   "CALL", "0xFF, $target",
                   []> {
  let Opcode = 0x3f;
}
}

let isReturn = 1, isTerminator = 1, isBarrier = 1, Uses = [R0, SP] in {
def RET : CatInst<(outs), (ins),
                  "RET", "",
                  [(ret)]> {
  let Opcode = 0x40;
}
}

// Pseudo-instructions
let isPseudo = 1 in {
def ADJCALLSTACKDOWN : CatInst<(outs), (ins i32imm:$amt1, i32imm:$amt2),
                               "#ADJCALLSTACKDOWN", "$amt1, $amt2",
                               [(callseq_start timm:$amt1, timm:$amt2)]>;

def ADJCALLSTACKUP : CatInst<(outs), (ins i32imm:$amt1, i32imm:$amt2),
                             "#ADJCALLSTACKUP", "$amt1, $amt2",
                             [(callseq_end timm:$amt1, timm:$amt2)]>;

// Select pseudo-instruction
def SELECT : CatInst<(outs GPR:$dst), (ins GPR:$lhs, GPR:$rhs, i32imm:$cond),
                     "#SELECT", "$dst, $lhs, $rhs, $cond",
                     []>;
}

//===----------------------------------------------------------------------===//
// Pattern matching support
//===----------------------------------------------------------------------===//

// Shifts, division, and modulo need to be handled specially or emulated
def : Pat<(shl GPR:$rs1, GPR:$rs2), (ADD_RR GPR:$rs1, GPR:$rs1)>;

// Select patterns
def : Pat<(selectcc GPR:$lhs, GPR:$rhs, GPR:$t, GPR:$f, SETEQ),
          (SELECT GPR:$t, GPR:$f, 0)>;

// Frame index
def : Pat<(i32 (frameindex tblockaddress:$label)), (MOV32_RI tblockaddress:$label)>;
