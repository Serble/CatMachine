//===-- CatInstrInfo.cpp - Cat Instruction Information -------------------===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file contains the Cat implementation of the TargetInstrInfo class.
//
//===----------------------------------------------------------------------===//

#include "CatInstrInfo.h"
#include "Cat.h"
#include "CatMachineFunctionInfo.h"
#include "CatTargetMachine.h"
#include "llvm/ADT/STLExtras.h"
#include "llvm/ADT/SmallVector.h"
#include "llvm/CodeGen/MachineFunctionPass.h"
#include "llvm/CodeGen/MachineInstrBuilder.h"
#include "llvm/CodeGen/MachineRegisterInfo.h"
#include "llvm/Support/ErrorHandling.h"
#include "llvm/Support/TargetRegistry.h"

using namespace llvm;

#define GET_INSTRINFO_CTOR_DTOR
#include "CatGenInstrInfo.inc"

CatInstrInfo::CatInstrInfo()
    : CatGenInstrInfo(Cat::ADJCALLSTACKDOWN, Cat::ADJCALLSTACKUP), RI() {}

void CatInstrInfo::copyPhysReg(MachineBasicBlock &MBB,
                                MachineBasicBlock::iterator MI,
                                const DebugLoc &DL, MCRegister DestReg,
                                MCRegister SrcReg, bool KillSrc) const {
  if (Cat::GPRRegClass.contains(DestReg, SrcReg)) {
    BuildMI(MBB, MI, DL, get(Cat::MOV32_RR), DestReg)
        .addReg(SrcReg, getKillRegState(KillSrc));
    return;
  }

  llvm_unreachable("Cannot copy between these registers");
}

void CatInstrInfo::storeRegToStackSlot(
    MachineBasicBlock &MBB, MachineBasicBlock::iterator MI, Register SrcReg,
    bool isKill, int FrameIndex, const TargetRegisterClass *RC,
    const TargetRegisterInfo *TRI) const {
  DebugLoc DL;
  if (MI != MBB.end())
    DL = MI->getDebugLoc();

  BuildMI(MBB, MI, DL, get(Cat::PUSH32))
      .addReg(SrcReg, getKillRegState(isKill));
}

void CatInstrInfo::loadRegFromStackSlot(
    MachineBasicBlock &MBB, MachineBasicBlock::iterator MI, Register DestReg,
    int FrameIndex, const TargetRegisterClass *RC,
    const TargetRegisterInfo *TRI) const {
  DebugLoc DL;
  if (MI != MBB.end())
    DL = MI->getDebugLoc();

  BuildMI(MBB, MI, DL, get(Cat::POP32), DestReg);
}

bool CatInstrInfo::expandPostRAPseudo(MachineInstr &MI) const {
  switch (MI.getOpcode()) {
  case Cat::SELECT: {
    // Expand SELECT pseudo instruction to conditional branches
    MachineBasicBlock &MBB = *MI.getParent();
    const DebugLoc &DL = MI.getDebugLoc();
    
    // For now, simple expansion
    unsigned DstReg = MI.getOperand(0).getReg();
    unsigned TrueReg = MI.getOperand(1).getReg();
    
    BuildMI(MBB, MI, DL, get(Cat::MOV32_RR), DstReg)
        .addReg(TrueReg);
    
    MI.eraseFromParent();
    return true;
  }
  }
  return false;
}
