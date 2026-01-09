//===-- CatInstrInfo.h - Cat Instruction Information -----------*- C++ -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file contains the Cat implementation of the TargetInstrInfo class.
//
//===----------------------------------------------------------------------===//

#ifndef LLVM_LIB_TARGET_CAT_CATINSTRINFO_H
#define LLVM_LIB_TARGET_CAT_CATINSTRINFO_H

#include "CatRegisterInfo.h"
#include "llvm/CodeGen/TargetInstrInfo.h"

#define GET_INSTRINFO_HEADER
#include "CatGenInstrInfo.inc"

namespace llvm {

class CatInstrInfo : public CatGenInstrInfo {
  const CatRegisterInfo RI;

public:
  CatInstrInfo();

  const CatRegisterInfo &getRegisterInfo() const { return RI; }

  void copyPhysReg(MachineBasicBlock &MBB, MachineBasicBlock::iterator MI,
                   const DebugLoc &DL, MCRegister DestReg, MCRegister SrcReg,
                   bool KillSrc) const override;

  void storeRegToStackSlot(MachineBasicBlock &MBB,
                          MachineBasicBlock::iterator MI, Register SrcReg,
                          bool isKill, int FrameIndex,
                          const TargetRegisterClass *RC,
                          const TargetRegisterInfo *TRI) const override;

  void loadRegFromStackSlot(MachineBasicBlock &MBB,
                           MachineBasicBlock::iterator MI, Register DestReg,
                           int FrameIndex, const TargetRegisterClass *RC,
                           const TargetRegisterInfo *TRI) const override;

  bool expandPostRAPseudo(MachineInstr &MI) const override;
};

} // end namespace llvm

#endif
