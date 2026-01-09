//===-- CatTargetInfo.h - Cat Target Implementation ------------*- C++ -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//

#ifndef LLVM_LIB_TARGET_CAT_TARGETINFO_CATTARGETINFO_H
#define LLVM_LIB_TARGET_CAT_TARGETINFO_CATTARGETINFO_H

namespace llvm {

class Target;

Target &getTheCatTarget();

} // end namespace llvm

#endif
