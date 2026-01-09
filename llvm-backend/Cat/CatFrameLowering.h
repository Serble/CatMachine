//===-- CatFrameLowering.h - Define frame lowering for Cat -----*- C++ -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This class implements Cat-specific bits of TargetFrameLowering class.
//
//===----------------------------------------------------------------------===//

#ifndef LLVM_LIB_TARGET_CAT_CATFRAMELOWERING_H
#define LLVM_LIB_TARGET_CAT_CATFRAMELOWERING_H

#include "llvm/CodeGen/TargetFrameLowering.h"

namespace llvm {
class CatSubtarget;

class CatFrameLowering : public TargetFrameLowering {
public:
  explicit CatFrameLowering(const CatSubtarget &STI)
      : TargetFrameLowering(StackGrowsDown,
                           /*StackAlignment=*/Align(4),
                           /*LocalAreaOffset=*/0) {}

  void emitPrologue(MachineFunction &MF, MachineBasicBlock &MBB) const override;
  void emitEpilogue(MachineFunction &MF, MachineBasicBlock &MBB) const override;

  bool hasFP(const MachineFunction &MF) const override;

  MachineBasicBlock::iterator
  eliminateCallFramePseudoInstr(MachineFunction &MF, MachineBasicBlock &MBB,
                                MachineBasicBlock::iterator MI) const override;
};

} // end namespace llvm

#endif
