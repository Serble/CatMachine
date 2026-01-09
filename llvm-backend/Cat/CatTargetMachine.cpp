//===-- CatTargetMachine.cpp - Define TargetMachine for Cat --------------===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//

#include "CatTargetMachine.h"
#include "Cat.h"
#include "TargetInfo/CatTargetInfo.h"
#include "llvm/CodeGen/Passes.h"
#include "llvm/CodeGen/TargetLoweringObjectFileImpl.h"
#include "llvm/CodeGen/TargetPassConfig.h"
#include "llvm/MC/TargetRegistry.h"
#include "llvm/Support/FormattedStream.h"

using namespace llvm;

extern "C" void LLVMInitializeCatTarget() {
  RegisterTargetMachine<CatTargetMachine> X(getTheCatTarget());
}

static std::string computeDataLayout(const Triple &TT) {
  // Cat is little endian, 32-bit pointers
  return "e-m:e-p:32:32-i32:32-i64:64-n32-S32";
}

CatTargetMachine::CatTargetMachine(const Target &T, const Triple &TT,
                                   StringRef CPU, StringRef FS,
                                   const TargetOptions &Options,
                                   Optional<Reloc::Model> RM,
                                   Optional<CodeModel::Model> CM,
                                   CodeGenOpt::Level OL, bool JIT)
    : LLVMTargetMachine(T, computeDataLayout(TT), TT, CPU, FS, Options,
                        RM.getValueOr(Reloc::Static),
                        CM.getValueOr(CodeModel::Small), OL),
      TLOF(std::make_unique<TargetLoweringObjectFileELF>()),
      Subtarget(TT, CPU, FS, *this) {
  initAsmInfo();
}

namespace {
class CatPassConfig : public TargetPassConfig {
public:
  CatPassConfig(CatTargetMachine &TM, PassManagerBase &PM)
      : TargetPassConfig(TM, PM) {}

  CatTargetMachine &getCatTargetMachine() const {
    return getTM<CatTargetMachine>();
  }

  bool addInstSelector() override;
  void addPreEmitPass() override;
};
} // namespace

TargetPassConfig *CatTargetMachine::createPassConfig(PassManagerBase &PM) {
  return new CatPassConfig(*this, PM);
}

bool CatPassConfig::addInstSelector() {
  addPass(createCatISelDag(getCatTargetMachine(), getOptLevel()));
  return false;
}

void CatPassConfig::addPreEmitPass() {
  // Add passes before emitting assembly
}
