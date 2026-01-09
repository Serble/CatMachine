//===-- CatFrameLowering.cpp - Cat Frame Information ---------------------===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file contains the Cat implementation of TargetFrameLowering class.
//
//===----------------------------------------------------------------------===//

#include "CatFrameLowering.h"
#include "CatInstrInfo.h"
#include "CatSubtarget.h"
#include "llvm/CodeGen/MachineFrameInfo.h"
#include "llvm/CodeGen/MachineFunction.h"
#include "llvm/CodeGen/MachineInstrBuilder.h"
#include "llvm/CodeGen/MachineRegisterInfo.h"

using namespace llvm;

bool CatFrameLowering::hasFP(const MachineFunction &MF) const {
  const MachineFrameInfo &MFI = MF.getFrameInfo();
  return MF.getTarget().Options.DisableFramePointerElim(MF) ||
         MFI.hasVarSizedObjects() || MFI.isFrameAddressTaken();
}

void CatFrameLowering::emitPrologue(MachineFunction &MF,
                                    MachineBasicBlock &MBB) const {
  const CatInstrInfo &TII =
      *static_cast<const CatInstrInfo *>(MF.getSubtarget().getInstrInfo());
  MachineFrameInfo &MFI = MF.getFrameInfo();
  MachineBasicBlock::iterator MBBI = MBB.begin();
  DebugLoc DL = MBBI != MBB.end() ? MBBI->getDebugLoc() : DebugLoc();

  // Get the number of bytes to allocate from the FrameInfo.
  uint64_t StackSize = MFI.getStackSize();

  if (StackSize == 0 && !MFI.adjustsStack())
    return;

  // Push callee-saved registers (R4-R7)
  BuildMI(MBB, MBBI, DL, TII.get(Cat::PUSH32)).addReg(R4);
  BuildMI(MBB, MBBI, DL, TII.get(Cat::PUSH32)).addReg(R5);
  BuildMI(MBB, MBBI, DL, TII.get(Cat::PUSH32)).addReg(R6);
  BuildMI(MBB, MBBI, DL, TII.get(Cat::PUSH32)).addReg(R7);

  if (StackSize) {
    // Adjust stack pointer: SUB SP, StackSize
    BuildMI(MBB, MBBI, DL, TII.get(Cat::SUB_RI), SP)
        .addReg(SP)
        .addImm(StackSize);
  }

  // Set frame pointer: MOV R7, SP
  BuildMI(MBB, MBBI, DL, TII.get(Cat::MOV32_RR), R7).addReg(SP);
}

void CatFrameLowering::emitEpilogue(MachineFunction &MF,
                                    MachineBasicBlock &MBB) const {
  const CatInstrInfo &TII =
      *static_cast<const CatInstrInfo *>(MF.getSubtarget().getInstrInfo());
  MachineFrameInfo &MFI = MF.getFrameInfo();
  MachineBasicBlock::iterator MBBI = MBB.getLastNonDebugInstr();
  DebugLoc DL = MBBI->getDebugLoc();

  uint64_t StackSize = MFI.getStackSize();

  // Restore stack pointer: MOV SP, R7
  BuildMI(MBB, MBBI, DL, TII.get(Cat::MOV32_RR), SP).addReg(R7);

  if (StackSize) {
    // Restore stack pointer: ADD SP, StackSize
    BuildMI(MBB, MBBI, DL, TII.get(Cat::ADD_RI), SP)
        .addReg(SP)
        .addImm(StackSize);
  }

  // Pop callee-saved registers (R7-R4, in reverse order)
  BuildMI(MBB, MBBI, DL, TII.get(Cat::POP32), R7);
  BuildMI(MBB, MBBI, DL, TII.get(Cat::POP32), R6);
  BuildMI(MBB, MBBI, DL, TII.get(Cat::POP32), R5);
  BuildMI(MBB, MBBI, DL, TII.get(Cat::POP32), R4);
}

MachineBasicBlock::iterator CatFrameLowering::eliminateCallFramePseudoInstr(
    MachineFunction &MF, MachineBasicBlock &MBB,
    MachineBasicBlock::iterator MI) const {
  const CatInstrInfo &TII =
      *static_cast<const CatInstrInfo *>(MF.getSubtarget().getInstrInfo());
  
  if (!hasReservedCallFrame(MF)) {
    int64_t Amount = MI->getOperand(0).getImm();

    if (Amount != 0) {
      if (MI->getOpcode() == Cat::ADJCALLSTACKDOWN) {
        // SUB SP, Amount
        BuildMI(MBB, MI, MI->getDebugLoc(), TII.get(Cat::SUB_RI), SP)
            .addReg(SP)
            .addImm(Amount);
      } else {
        // ADD SP, Amount
        BuildMI(MBB, MI, MI->getDebugLoc(), TII.get(Cat::ADD_RI), SP)
            .addReg(SP)
            .addImm(Amount);
      }
    }
  }

  return MBB.erase(MI);
}
