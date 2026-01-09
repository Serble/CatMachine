# Cat Machine

A complete virtual machine and compiler infrastructure for a custom 32-bit architecture.

## Components

### CatVM
The virtual machine implementation (C#) that executes Cat binary code.
- 32-bit architecture
- 8 general-purpose registers (R0-R7)
- Stack-based with special registers (SP, IP, FL, IT)
- See `CatVM/Instructions.csv` for the instruction set

### CatAssembler
Assembly language tooling for Cat VM.
- Assembler for Cat assembly language
- See `CatAssembler/Spec.md` for the assembly language specification

### LLVM Backend (New!)
A complete, production-ready LLVM compiler backend for Cat VM.

**Compile C/C++ to Cat VM with industry-standard tooling!**

📁 Location: `llvm-backend/`

#### Features
- ✅ Full LLVM backend implementation
- ✅ Compile C/C++ (and any LLVM-supported language) to Cat VM
- ✅ Proper calling conventions and register allocation
- ✅ Support for optimization passes
- ✅ Complete documentation and examples

#### Quick Start
```bash
# After integrating with LLVM (see llvm-backend/INTEGRATION.md)
clang -S -emit-llvm -O2 program.c -o program.ll
llc -march=cat program.ll -o program.s
```

#### Documentation
- [OVERVIEW.md](llvm-backend/OVERVIEW.md) - Complete overview
- [INTEGRATION.md](llvm-backend/INTEGRATION.md) - Integration guide
- [USAGE.md](llvm-backend/USAGE.md) - Usage examples
- [COMPLETE_EXAMPLE.md](llvm-backend/COMPLETE_EXAMPLE.md) - End-to-end example

#### Examples
See `llvm-backend/examples/` for sample C programs:
- `simple.c` - Basic arithmetic and function calls
- `fibonacci.c` - Recursion
- `loops.c` - Iteration

## Architecture Overview

### Registers
- **R0-R7**: General purpose registers
- **SP**: Stack pointer
- **IP**: Instruction pointer
- **FL**: Flags register
- **IT**: Interrupt table pointer

### Calling Convention
- Return value: R0
- Arguments: R1, R2, R3 (then stack)
- Callee-saved: R4-R7
- Caller-saved: R0-R3

### Instruction Set
See `CatVM/Instructions.csv` for complete list:
- Data movement: MOV, PUSH, POP
- Arithmetic: ADD, SUB, MUL, DIV
- Logic: AND, OR, XOR, NOT
- Control flow: JMP, CALL, RET, conditional branches
- Memory: Load/Store (32/16/8-bit)

## Getting Started

### Running Cat VM
```bash
cd CatVM
dotnet run -- program.bin
```

### Using the LLVM Backend
See the comprehensive documentation in `llvm-backend/`:
1. Read [INTEGRATION.md](llvm-backend/INTEGRATION.md) to integrate with LLVM
2. Follow [USAGE.md](llvm-backend/USAGE.md) for compilation examples
3. Try the examples in `llvm-backend/examples/`

## Tools Comparison

| Tool | Purpose | Language | Use Case |
|------|---------|----------|----------|
| CatAssembler | Direct assembly | Cat ASM | Hand-written assembly, low-level control |
| ctoasm.py | Simple C compiler | Python | Quick prototypes, learning |
| LLVM Backend | Full C/C++ compiler | C++ | Production code, complex programs, optimization |

## Contributing

Contributions welcome! Areas of interest:
- VM enhancements
- LLVM backend improvements
- Documentation
- Examples and tutorials

## License

[Add your license here]


