add_llvm_component_library(LLVMCatAsmPrinter
  CatInstPrinter.cpp

  LINK_COMPONENTS
  MC
  Support
  )
