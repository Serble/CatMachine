//===-- CatRegisterInfo.cpp - Cat Register Information -------------------===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file contains the Cat implementation of the TargetRegisterInfo class.
//
//===----------------------------------------------------------------------===//

#include "CatRegisterInfo.h"
#include "Cat.h"
#include "CatFrameLowering.h"
#include "CatSubtarget.h"
#include "llvm/CodeGen/MachineFrameInfo.h"
#include "llvm/CodeGen/MachineFunction.h"
#include "llvm/CodeGen/MachineInstrBuilder.h"
#include "llvm/CodeGen/RegisterScavenging.h"
#include "llvm/CodeGen/TargetFrameLowering.h"
#include "llvm/CodeGen/TargetInstrInfo.h"
#include "llvm/Support/ErrorHandling.h"

using namespace llvm;

#define GET_REGINFO_TARGET_DESC
#include "CatGenRegisterInfo.inc"

CatRegisterInfo::CatRegisterInfo() : CatGenRegisterInfo(Cat::R0) {}

const MCPhysReg *
CatRegisterInfo::getCalleeSavedRegs(const MachineFunction *MF) const {
  return CSR_SaveList;
}

BitVector CatRegisterInfo::getReservedRegs(const MachineFunction &MF) const {
  BitVector Reserved(getNumRegs());

  // Reserve special registers
  Reserved.set(Cat::SP);
  Reserved.set(Cat::IP);
  Reserved.set(Cat::FL);
  Reserved.set(Cat::IT);
  Reserved.set(Cat::R7); // Frame pointer

  return Reserved;
}

bool CatRegisterInfo::eliminateFrameIndex(MachineBasicBlock::iterator MI,
                                          int SPAdj, unsigned FIOperandNum,
                                          RegScavenger *RS) const {
  MachineInstr &MI_ref = *MI;
  MachineFunction &MF = *MI->getParent()->getParent();
  const TargetFrameLowering *TFI = MF.getSubtarget().getFrameLowering();
  
  int FrameIndex = MI->getOperand(FIOperandNum).getIndex();
  int Offset = MF.getFrameInfo().getObjectOffset(FrameIndex) +
               MI->getOperand(FIOperandNum + 1).getImm();
  
  // Replace frame index with R7 (frame pointer) + offset
  MI->getOperand(FIOperandNum).ChangeToRegister(Cat::R7, false);
  MI->getOperand(FIOperandNum + 1).ChangeToImmediate(Offset);
  
  return false;
}

Register CatRegisterInfo::getFrameRegister(const MachineFunction &MF) const {
  return Cat::R7;
}
