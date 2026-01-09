//===-- CatMCTargetDesc.h - Cat Target Descriptions ------------*- C++ -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file provides Cat specific target descriptions.
//
//===----------------------------------------------------------------------===//

#ifndef LLVM_LIB_TARGET_CAT_MCTARGETDESC_CATMCTARGETDESC_H
#define LLVM_LIB_TARGET_CAT_MCTARGETDESC_CATMCTARGETDESC_H

#include "llvm/Support/DataTypes.h"
#include <memory>

namespace llvm {
class Target;
class Triple;

Target &getTheCatTarget();

} // end namespace llvm

// Defines symbolic names for Cat registers.
#define GET_REGINFO_ENUM
#include "CatGenRegisterInfo.inc"

// Defines symbolic names for Cat instructions.
#define GET_INSTRINFO_ENUM
#include "CatGenInstrInfo.inc"

#define GET_SUBTARGETINFO_ENUM
#include "CatGenSubtargetInfo.inc"

#endif
