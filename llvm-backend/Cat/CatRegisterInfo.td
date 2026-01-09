//===-- CatRegisterInfo.td - Cat Register defs -------------*- tablegen -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// Declarations that describe the Cat register file
//
//===----------------------------------------------------------------------===//

//===----------------------------------------------------------------------===//
// Class definitions
//===----------------------------------------------------------------------===//

class CatReg<bits<4> Enc, string n, list<string> alt = []> : Register<n> {
  let HWEncoding{3-0} = Enc;
  let AltNames = alt;
}

//===----------------------------------------------------------------------===//
// Integer registers
//===----------------------------------------------------------------------===//

// General purpose registers
def R0 : CatReg<0, "r0">, DwarfRegNum<[0]>;
def R1 : CatReg<1, "r1">, DwarfRegNum<[1]>;
def R2 : CatReg<2, "r2">, DwarfRegNum<[2]>;
def R3 : CatReg<3, "r3">, DwarfRegNum<[3]>;
def R4 : CatReg<4, "r4">, DwarfRegNum<[4]>;
def R5 : CatReg<5, "r5">, DwarfRegNum<[5]>;
def R6 : CatReg<6, "r6">, DwarfRegNum<[6]>;
def R7 : CatReg<7, "r7">, DwarfRegNum<[7]>;

// Special purpose registers
def SP : CatReg<8, "sp">, DwarfRegNum<[8]>;
def IP : CatReg<9, "ip">, DwarfRegNum<[9]>;
def FL : CatReg<10, "fl">, DwarfRegNum<[10]>;
def IT : CatReg<11, "it">, DwarfRegNum<[11]>;

//===----------------------------------------------------------------------===//
// Register Classes
//===----------------------------------------------------------------------===//

// General Purpose Registers - can be used for most operations
def GPR : RegisterClass<"Cat", [i32], 32, (add
  R0, R1, R2, R3, R4, R5, R6, R7
)>;

// All registers including special ones
def AllRegs : RegisterClass<"Cat", [i32], 32, (add
  GPR, SP, IP, FL, IT
)>;
