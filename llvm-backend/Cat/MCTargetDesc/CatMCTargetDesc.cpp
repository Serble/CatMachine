//===-- CatMCTargetDesc.cpp - Cat Target Descriptions --------------------===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file provides Cat specific target descriptions.
//
//===----------------------------------------------------------------------===//

#include "CatMCTargetDesc.h"
#include "CatInstPrinter.h"
#include "CatMCAsmInfo.h"
#include "TargetInfo/CatTargetInfo.h"
#include "llvm/MC/MCInstrInfo.h"
#include "llvm/MC/MCRegisterInfo.h"
#include "llvm/MC/MCSubtargetInfo.h"
#include "llvm/Support/TargetRegistry.h"

using namespace llvm;

#define GET_INSTRINFO_MC_DESC
#include "CatGenInstrInfo.inc"

#define GET_SUBTARGETINFO_MC_DESC
#include "CatGenSubtargetInfo.inc"

#define GET_REGINFO_MC_DESC
#include "CatGenRegisterInfo.inc"

static MCInstrInfo *createCatMCInstrInfo() {
  MCInstrInfo *X = new MCInstrInfo();
  InitCatMCInstrInfo(X);
  return X;
}

static MCRegisterInfo *createCatMCRegisterInfo(const Triple &TT) {
  MCRegisterInfo *X = new MCRegisterInfo();
  InitCatMCRegisterInfo(X, Cat::R0);
  return X;
}

static MCSubtargetInfo *createCatMCSubtargetInfo(const Triple &TT,
                                                 StringRef CPU, StringRef FS) {
  return createCatMCSubtargetInfoImpl(TT, CPU, FS);
}

static MCAsmInfo *createCatMCAsmInfo(const MCRegisterInfo &MRI,
                                     const Triple &TT,
                                     const MCTargetOptions &Options) {
  return new CatMCAsmInfo(TT);
}

static MCInstPrinter *createCatMCInstPrinter(const Triple &T,
                                             unsigned SyntaxVariant,
                                             const MCAsmInfo &MAI,
                                             const MCInstrInfo &MII,
                                             const MCRegisterInfo &MRI) {
  return new CatInstPrinter(MAI, MII, MRI);
}

extern "C" void LLVMInitializeCatTargetMC() {
  // Register the MC asm info.
  RegisterMCAsmInfoFn X(getTheCatTarget(), createCatMCAsmInfo);

  // Register the MC instruction info.
  TargetRegistry::RegisterMCInstrInfo(getTheCatTarget(), createCatMCInstrInfo);

  // Register the MC register info.
  TargetRegistry::RegisterMCRegInfo(getTheCatTarget(),
                                    createCatMCRegisterInfo);

  // Register the MC subtarget info.
  TargetRegistry::RegisterMCSubtargetInfo(getTheCatTarget(),
                                          createCatMCSubtargetInfo);

  // Register the MCInstPrinter
  TargetRegistry::RegisterMCInstPrinter(getTheCatTarget(),
                                        createCatMCInstPrinter);
}
