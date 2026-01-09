//===-- Cat.h - Top-level interface for Cat representation -----*- C++ -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file contains the entry points for global functions defined in the LLVM
// Cat back-end.
//
//===----------------------------------------------------------------------===//

#ifndef LLVM_LIB_TARGET_CAT_CAT_H
#define LLVM_LIB_TARGET_CAT_CAT_H

#include "MCTargetDesc/CatMCTargetDesc.h"
#include "llvm/Target/TargetMachine.h"

namespace llvm {
class CatTargetMachine;
class FunctionPass;

FunctionPass *createCatISelDag(CatTargetMachine &TM,
                               CodeGenOpt::Level OptLevel);

} // end namespace llvm

#endif
