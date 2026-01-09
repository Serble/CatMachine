# LLVM Backend for Cat VM Architecture

This directory contains a complete LLVM backend implementation for the Cat VM architecture as defined in `CatVM/Instructions.csv` and `CatVM/Registers.csv`.

## Architecture Overview

Cat VM is a 32-bit architecture with the following characteristics:

### Registers
- **General Purpose Registers**: R0-R7 (8 registers)
- **Special Registers**:
  - SP (Stack Pointer)
  - IP (Instruction Pointer)
  - FL (Flags Register)
  - IT (Interrupt Table Pointer)

### Calling Convention
- **Return Value**: R0
- **Arguments**: First three in R1, R2, R3; rest on stack
- **Callee-Saved**: R4-R7
- **Caller-Saved**: R0-R3

### Data Layout
- Little-endian
- 32-bit pointers
- 32-bit natural alignment

## Backend Structure

### TableGen Definitions
- `Cat.td` - Main target definition
- `CatRegisterInfo.td` - Register definitions and classes
- `CatInstrInfo.td` - Instruction definitions and patterns
- `CatCallingConv.td` - Calling convention specifications

### C++ Implementation
- `CatTargetMachine.*` - Target machine implementation
- `CatSubtarget.*` - Subtarget features and information
- `CatInstrInfo.*` - Instruction information and manipulation
- `CatRegisterInfo.*` - Register information and frame handling
- `CatFrameLowering.*` - Stack frame management (prologue/epilogue)
- `CatISelLowering.*` - Instruction selection lowering (DAG to DAG)
- `CatISelDAGToDAG.*` - Pattern-based instruction selection

### MC Layer (Machine Code)
- `MCTargetDesc/CatMCTargetDesc.*` - MC layer initialization
- `MCTargetDesc/CatMCAsmInfo.*` - Assembly syntax information
- `InstPrinter/CatInstPrinter.*` - Pretty-printing assembly output

### Target Registration
- `TargetInfo/CatTargetInfo.*` - Target registration with LLVM

## Integration with LLVM

To integrate this backend into LLVM:

1. Copy the `Cat` directory to `llvm/lib/Target/`
2. Add `Cat` to `llvm/lib/Target/CMakeLists.txt`:
   ```cmake
   add_subdirectory(Cat)
   ```
3. Add to the target list in `llvm/lib/Target/LLVMBuild.txt`
4. Rebuild LLVM

## Building

```bash
cd llvm-build
cmake ../llvm -DLLVM_TARGETS_TO_BUILD="Cat" -DCMAKE_BUILD_TYPE=Release
make -j$(nproc)
```

## Usage

### Compiling C to Cat Assembly

```bash
# Compile C to LLVM IR
clang -S -emit-llvm -O2 example.c -o example.ll

# Compile LLVM IR to Cat assembly
llc -march=cat example.ll -o example.s

# Or in one step
clang -target cat -S example.c -o example.s
```

## Supported Features

### Instructions
- **Data Movement**: MOV (32/16/8-bit), PUSH, POP
- **Arithmetic**: ADD, SUB, UMUL, IMUL, UDIV, IDIV
- **Logic**: AND, OR, XOR, NOT
- **Control Flow**: JMP, CALL, RET, Conditional jumps (JZ, JNZ, JUL, etc.)
- **Comparison**: CMP
- **Memory**: Load/Store with various sizes

### Limitations
- No hardware shift instructions (requires software emulation)
- No floating-point support
- Limited to 32-bit integers
- Stack-based argument passing for more than 3 arguments

## Example Programs

See the `examples/` directory for sample C programs and their compiled Cat assembly output.

## Future Enhancements

- Assembly parser for integrated assembler
- ELF object file emission
- Debugging information (DWARF)
- Optimization passes specific to Cat architecture
- LLVM IR interpreter support
