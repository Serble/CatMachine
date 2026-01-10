//===-- CatMCAsmInfo.cpp - Cat asm properties ----------------------------===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//

#include "CatMCAsmInfo.h"
#include "llvm/ADT/Triple.h"

using namespace llvm;

void CatMCAsmInfo::anchor() {}

CatMCAsmInfo::CatMCAsmInfo(const Triple &TT) {
  IsLittleEndian = true;
  CodePointerSize = 4;
  CalleeSaveStackSlotSize = 4;
  
  CommentString = ";";
  
  SupportsDebugInformation = false;
  
  // Cat assembly uses @ for memory access
  PrivateGlobalPrefix = ".L";
  PrivateLabelPrefix = ".L";
  
  Data8bitsDirective = "\tD8\t";
  Data16bitsDirective = "\tD16\t";
  Data32bitsDirective = "\tD32\t";
  Data64bitsDirective = nullptr; // Not supported
  
  ZeroDirective = "\tD32\t";
  
  HasSingleParameterDotFile = false;
  HasDotTypeDotSizeDirective = false;
  
  UseIntegratedAssembler = false;
}
