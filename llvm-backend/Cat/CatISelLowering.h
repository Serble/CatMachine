//===-- CatISelLowering.h - Cat DAG Lowering Interface ---------*- C++ -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file defines the interfaces that Cat uses to lower LLVM code into a
// selection DAG.
//
//===----------------------------------------------------------------------===//

#ifndef LLVM_LIB_TARGET_CAT_CATISELLOWERING_H
#define LLVM_LIB_TARGET_CAT_CATISELLOWERING_H

#include "Cat.h"
#include "llvm/CodeGen/SelectionDAG.h"
#include "llvm/CodeGen/TargetLowering.h"

namespace llvm {
class CatSubtarget;

class CatTargetLowering : public TargetLowering {
  const CatSubtarget &Subtarget;

public:
  explicit CatTargetLowering(const TargetMachine &TM,
                            const CatSubtarget &STI);

  SDValue LowerOperation(SDValue Op, SelectionDAG &DAG) const override;

  const char *getTargetNodeName(unsigned Opcode) const override;

  SDValue LowerFormalArguments(SDValue Chain, CallingConv::ID CallConv,
                               bool IsVarArg,
                               const SmallVectorImpl<ISD::InputArg> &Ins,
                               const SDLoc &DL, SelectionDAG &DAG,
                               SmallVectorImpl<SDValue> &InVals) const override;

  SDValue LowerReturn(SDValue Chain, CallingConv::ID CallConv, bool IsVarArg,
                     const SmallVectorImpl<ISD::OutputArg> &Outs,
                     const SmallVectorImpl<SDValue> &OutVals, const SDLoc &DL,
                     SelectionDAG &DAG) const override;

  SDValue LowerCall(TargetLowering::CallLoweringInfo &CLI,
                   SmallVectorImpl<SDValue> &InVals) const override;

private:
  SDValue LowerBR_CC(SDValue Op, SelectionDAG &DAG) const;
  SDValue LowerSELECT_CC(SDValue Op, SelectionDAG &DAG) const;
};

} // end namespace llvm

#endif
