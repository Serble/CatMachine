//===-- CatISelDAGToDAG.cpp - A Dag to Dag Inst Selector for Cat ---------===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This file defines an instruction selector for the Cat target.
//
//===----------------------------------------------------------------------===//

#include "Cat.h"
#include "CatTargetMachine.h"
#include "llvm/CodeGen/SelectionDAGISel.h"
#include "llvm/Support/Debug.h"
#include "llvm/Support/raw_ostream.h"

using namespace llvm;

#define DEBUG_TYPE "cat-isel"

namespace {

class CatDAGToDAGISel : public SelectionDAGISel {
public:
  explicit CatDAGToDAGISel(CatTargetMachine &TM, CodeGenOpt::Level OptLevel)
      : SelectionDAGISel(TM, OptLevel) {}

  StringRef getPassName() const override {
    return "Cat DAG->DAG Pattern Instruction Selection";
  }

  void Select(SDNode *N) override;

#include "CatGenDAGISel.inc"
};

} // end anonymous namespace

void CatDAGToDAGISel::Select(SDNode *N) {
  // If we have a custom node, we already selected it
  if (N->isMachineOpcode()) {
    N->setNodeId(-1);
    return;
  }

  // Select the default instruction
  SelectCode(N);
}

FunctionPass *llvm::createCatISelDag(CatTargetMachine &TM,
                                     CodeGenOpt::Level OptLevel) {
  return new CatDAGToDAGISel(TM, OptLevel);
}
