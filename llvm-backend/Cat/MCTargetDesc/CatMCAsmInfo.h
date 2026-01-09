//===-- CatMCAsmInfo.h - Cat asm properties ------------------*- C++ -*--===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file contains the declaration of the CatMCAsmInfo class.
//
//===----------------------------------------------------------------------===//

#ifndef LLVM_LIB_TARGET_CAT_MCTARGETDESC_CATMCASMINFO_H
#define LLVM_LIB_TARGET_CAT_MCTARGETDESC_CATMCASMINFO_H

#include "llvm/MC/MCAsmInfoELF.h"

namespace llvm {
class Triple;

class CatMCAsmInfo : public MCAsmInfoELF {
  void anchor() override;

public:
  explicit CatMCAsmInfo(const Triple &TT);
};

} // end namespace llvm

#endif
