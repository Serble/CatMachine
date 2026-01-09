//===-- CatTargetInfo.cpp - Cat Target Implementation --------------------===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//

#include "CatTargetInfo.h"
#include "llvm/MC/TargetRegistry.h"

using namespace llvm;

Target &llvm::getTheCatTarget() {
  static Target TheCatTarget;
  return TheCatTarget;
}

extern "C" void LLVMInitializeCatTargetInfo() {
  RegisterTarget<Triple::UnknownArch, /*HasJIT=*/false> X(
      getTheCatTarget(), "cat", "Cat VM", "Cat");
}
