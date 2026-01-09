//===-- Cat.td - Describe the Cat Target Machine ----------*- tablegen -*-===//
//
// Part of the LLVM Project
//
//===----------------------------------------------------------------------===//
//
// This is the top-level entry point for the Cat target.
//
//===----------------------------------------------------------------------===//

//===----------------------------------------------------------------------===//
// Target-independent interfaces which we are implementing
//===----------------------------------------------------------------------===//

include "llvm/Target/Target.td"

//===----------------------------------------------------------------------===//
// Cat Subtarget features
//===----------------------------------------------------------------------===//

def FeatureStdExtM : SubtargetFeature<"m", "HasStdExtM", "true",
                                       "Standard Extension for Integer Multiplication and Division">;

//===----------------------------------------------------------------------===//
// Registers, calling conventions, instruction descriptions
//===----------------------------------------------------------------------===//

include "CatRegisterInfo.td"
include "CatCallingConv.td"
include "CatInstrInfo.td"

//===----------------------------------------------------------------------===//
// Cat processors
//===----------------------------------------------------------------------===//

def : ProcessorModel<"generic-cat", NoSchedModel, []>;

//===----------------------------------------------------------------------===//
// Assembly writer
//===----------------------------------------------------------------------===//

def CatAsmWriter : AsmWriter {
  string AsmWriterClassName  = "InstPrinter";
  int PassSubtarget = 1;
  int Variant = 0;
}

//===----------------------------------------------------------------------===//
// Declare the target
//===----------------------------------------------------------------------===//

def CatInstrInfo : InstrInfo;

def Cat : Target {
  let InstructionSet = CatInstrInfo;
  let AssemblyWriters = [CatAsmWriter];
}
