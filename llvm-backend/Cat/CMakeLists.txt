# LLVM backend for Cat VM architecture

set(LLVM_TARGET_DEFINITIONS Cat.td)

# Generate register info
tablegen(LLVM CatGenRegisterInfo.inc -gen-register-info)

# Generate instruction info
tablegen(LLVM CatGenInstrInfo.inc -gen-instr-info)

# Generate assembly writer
tablegen(LLVM CatGenAsmWriter.inc -gen-asm-writer)

# Generate DAG instruction selector
tablegen(LLVM CatGenDAGISel.inc -gen-dag-isel)

# Generate calling conventions
tablegen(LLVM CatGenCallingConv.inc -gen-callingconv)

# Generate subtarget info
tablegen(LLVM CatGenSubtargetInfo.inc -gen-subtarget)

add_public_tablegen_target(CatCommonTableGen)

# Create the Cat component group
add_llvm_component_group(Cat)

# Cat target sources
add_llvm_target(CatCodeGen
  CatTargetMachine.cpp
  CatSubtarget.cpp
  CatInstrInfo.cpp
  CatRegisterInfo.cpp
  CatFrameLowering.cpp
  CatISelLowering.cpp
  CatISelDAGToDAG.cpp

  LINK_COMPONENTS
  Analysis
  AsmPrinter
  CodeGen
  Core
  MC
  SelectionDAG
  Support
  Target
  GlobalISel
  )

add_subdirectory(TargetInfo)
add_subdirectory(MCTargetDesc)
add_subdirectory(InstPrinter)
