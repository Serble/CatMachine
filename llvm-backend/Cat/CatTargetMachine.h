//===-- CatTargetMachine.h - Define TargetMachine for Cat ------*- C++ -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file declares the Cat specific subclass of TargetMachine.
//
//===----------------------------------------------------------------------===//

#ifndef LLVM_LIB_TARGET_CAT_CATTARGETMACHINE_H
#define LLVM_LIB_TARGET_CAT_CATTARGETMACHINE_H

#include "CatSubtarget.h"
#include "llvm/Target/TargetMachine.h"

namespace llvm {

class CatTargetMachine : public LLVMTargetMachine {
  std::unique_ptr<TargetLoweringObjectFile> TLOF;
  CatSubtarget Subtarget;

public:
  CatTargetMachine(const Target &T, const Triple &TT, StringRef CPU,
                   StringRef FS, const TargetOptions &Options,
                   Optional<Reloc::Model> RM, Optional<CodeModel::Model> CM,
                   CodeGenOpt::Level OL, bool JIT);

  const CatSubtarget *getSubtargetImpl(const Function &) const override {
    return &Subtarget;
  }

  TargetPassConfig *createPassConfig(PassManagerBase &PM) override;

  TargetLoweringObjectFile *getObjFileLowering() const override {
    return TLOF.get();
  }
};

} // end namespace llvm

#endif
