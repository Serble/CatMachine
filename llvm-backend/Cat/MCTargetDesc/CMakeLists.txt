add_llvm_component_library(LLVMCatDesc
  CatMCTargetDesc.cpp
  CatMCAsmInfo.cpp

  LINK_COMPONENTS
  MC
  CatInfo
  Support
  )
