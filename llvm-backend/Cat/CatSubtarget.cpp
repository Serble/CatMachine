//===-- CatSubtarget.cpp - Cat Subtarget Information ---------------------===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//

#include "CatSubtarget.h"
#include "Cat.h"
#include "llvm/MC/TargetRegistry.h"

using namespace llvm;

#define DEBUG_TYPE "cat-subtarget"

#define GET_SUBTARGETINFO_TARGET_DESC
#define GET_SUBTARGETINFO_CTOR
#include "CatGenSubtargetInfo.inc"

void CatSubtarget::anchor() {}

CatSubtarget::CatSubtarget(const Triple &TT, const std::string &CPU,
                           const std::string &FS, const TargetMachine &TM)
    : CatGenSubtargetInfo(TT, CPU, FS), InstrInfo(), RegInfo(),
      TLInfo(TM, *this), FrameLowering(*this) {}
