//===-- CatMachineFunctionInfo.h - Cat machine func info --------*- C++ -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file declares Cat-specific per-machine-function information.
//
//===----------------------------------------------------------------------===//

#ifndef LLVM_LIB_TARGET_CAT_CATMACHINEFUNCTIONINFO_H
#define LLVM_LIB_TARGET_CAT_CATMACHINEFUNCTIONINFO_H

#include "llvm/CodeGen/MachineFunction.h"

namespace llvm {

class CatMachineFunctionInfo : public MachineFunctionInfo {
private:
  unsigned VarArgsFrameIndex = 0;
  unsigned ReturnAddrIndex = 0;

public:
  CatMachineFunctionInfo() = default;

  explicit CatMachineFunctionInfo(MachineFunction &MF) {}

  unsigned getVarArgsFrameIndex() const { return VarArgsFrameIndex; }
  void setVarArgsFrameIndex(unsigned Index) { VarArgsFrameIndex = Index; }

  unsigned getReturnAddrIndex() const { return ReturnAddrIndex; }
  void setReturnAddrIndex(unsigned Index) { ReturnAddrIndex = Index; }
};

} // end namespace llvm

#endif
