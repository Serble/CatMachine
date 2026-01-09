//===-- CatCallingConv.td - Calling Conventions Cat -------*- tablegen -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This describes the calling conventions for the Cat architecture.
//
//===----------------------------------------------------------------------===//

//===----------------------------------------------------------------------===//
// Cat Return Value Calling Convention
//===----------------------------------------------------------------------===//

def RetCC_Cat : CallingConv<[
  // i32 is returned in register R0
  CCIfType<[i32], CCAssignToReg<[R0]>>,
  
  // i16 is promoted to i32 and returned in R0
  CCIfType<[i16], CCPromoteToType<i32>>,
  
  // i8 is promoted to i32 and returned in R0
  CCIfType<[i8], CCPromoteToType<i32>>
]>;

//===----------------------------------------------------------------------===//
// Cat Argument Calling Convention
//===----------------------------------------------------------------------===//

def CC_Cat : CallingConv<[
  // Promote i8/i16 to i32
  CCIfType<[i8, i16], CCPromoteToType<i32>>,
  
  // First three arguments are passed in R1, R2, R3
  CCIfType<[i32], CCAssignToReg<[R1, R2, R3]>>,
  
  // Arguments beyond the third are passed on the stack with 4-byte alignment
  CCIfType<[i32], CCAssignToStack<4, 4>>
]>;

//===----------------------------------------------------------------------===//
// Callee-saved Registers
//===----------------------------------------------------------------------===//

def CSR : CalleeSavedRegs<(add R4, R5, R6, R7)>;
