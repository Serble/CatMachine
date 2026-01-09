//===-- CatSubtarget.h - Define Subtarget for the Cat ----------*- C++ -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file declares the Cat specific subclass of TargetSubtargetInfo.
//
//===----------------------------------------------------------------------===//

#ifndef LLVM_LIB_TARGET_CAT_CATSUBTARGET_H
#define LLVM_LIB_TARGET_CAT_CATSUBTARGET_H

#include "CatFrameLowering.h"
#include "CatISelLowering.h"
#include "CatInstrInfo.h"
#include "CatRegisterInfo.h"
#include "llvm/CodeGen/SelectionDAGTargetInfo.h"
#include "llvm/CodeGen/TargetSubtargetInfo.h"
#include "llvm/IR/DataLayout.h"

#define GET_SUBTARGETINFO_HEADER
#include "CatGenSubtargetInfo.inc"

namespace llvm {
class StringRef;

class CatSubtarget : public CatGenSubtargetInfo {
  virtual void anchor();
  CatInstrInfo InstrInfo;
  CatRegisterInfo RegInfo;
  CatTargetLowering TLInfo;
  CatFrameLowering FrameLowering;

public:
  CatSubtarget(const Triple &TT, const std::string &CPU,
               const std::string &FS, const TargetMachine &TM);

  void ParseSubtargetFeatures(StringRef CPU, StringRef FS);

  const CatInstrInfo *getInstrInfo() const override { return &InstrInfo; }
  const CatRegisterInfo *getRegisterInfo() const override { return &RegInfo; }
  const CatTargetLowering *getTargetLowering() const override {
    return &TLInfo;
  }
  const CatFrameLowering *getFrameLowering() const override {
    return &FrameLowering;
  }
};

} // end namespace llvm

#endif
