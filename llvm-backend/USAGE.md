# Compiling C Programs to Cat VM Using LLVM

This guide demonstrates how to use the LLVM backend to compile C programs for the Cat VM architecture.

## Prerequisites

1. LLVM with Cat backend integrated (see main README.md for integration steps)
2. Clang compiler
3. Cat VM assembler and runtime

## Method 1: Using LLVM's llc Tool (Recommended for Development)

This method gives you the most control and is useful for debugging.

### Step 1: Compile C to LLVM IR

```bash
clang -S -emit-llvm -O2 simple.c -o simple.ll
```

This produces LLVM Intermediate Representation (IR) which is a platform-independent format.

### Step 2: Compile LLVM IR to Cat Assembly

```bash
llc -march=cat simple.ll -o simple.s
```

The `-march=cat` flag tells LLVM to use the Cat backend.

### Optional: View the LLVM IR

```bash
cat simple.ll
```

### Optional: Optimization Levels

```bash
# No optimization
clang -S -emit-llvm -O0 simple.c -o simple.ll

# Full optimization
clang -S -emit-llvm -O3 simple.c -o simple.ll

# Size optimization
clang -S -emit-llvm -Os simple.c -o simple.ll
```

## Method 2: Using Clang Directly (When Fully Integrated)

Once the Cat target is fully registered with Clang:

```bash
clang -target cat -S simple.c -o simple.s
```

Or to compile and assemble in one step:

```bash
clang -target cat -c simple.c -o simple.o
```

## Method 3: Cross-Compilation Workflow

For a complete cross-compilation setup:

```bash
# Set up cross-compilation environment
export CAT_SYSROOT=/path/to/cat/sysroot
export CAT_TOOLCHAIN=/path/to/cat/tools

# Compile with full cross-compilation options
clang -target cat \
      --sysroot=$CAT_SYSROOT \
      -S simple.c -o simple.s
```

## Working with the Examples

### Example 1: Simple Arithmetic

```bash
cd examples

# Compile to LLVM IR
clang -S -emit-llvm -O2 simple.c -o simple.ll

# Compile to Cat assembly
llc -march=cat simple.ll -o simple.s

# View the generated assembly
cat simple.s
```

Expected output structure:
```asm
; Prologue with frame setup
; Function calls following Cat calling convention
; Epilogue with frame teardown
; Return instruction
```

### Example 2: Fibonacci (Recursion)

```bash
clang -S -emit-llvm -O2 fibonacci.c -o fibonacci.ll
llc -march=cat fibonacci.ll -o fibonacci.s
```

This demonstrates:
- Recursive function calls
- Proper stack frame management
- Conditional branches
- Return value handling

### Example 3: Loops

```bash
clang -S -emit-llvm -O2 loops.c -o loops.ll
llc -march=cat loops.ll -o loops.s
```

This demonstrates:
- Loop structures compiled to jumps
- Variable accumulation
- Multiple function definitions

## Understanding the Generated Assembly

### Register Usage

According to Cat calling convention:
- `r0`: Return values
- `r1-r3`: First three function arguments
- `r4-r7`: Callee-saved (preserved across calls)
- `sp`: Stack pointer
- `r7`: Frame pointer

### Example Assembly Pattern

```asm
function_name:
    ; Prologue
    PUSH r4           ; Save callee-saved registers
    PUSH r5
    PUSH r6
    PUSH r7
    SUB sp, <framesize>  ; Allocate stack frame
    MOV r7, sp        ; Set frame pointer
    
    ; Function body
    MOV r1, <arg1>    ; Load arguments
    CALL 0xFF, other_function
    
    ; Epilogue
    MOV sp, r7        ; Restore stack pointer
    ADD sp, <framesize>
    POP r7            ; Restore callee-saved registers
    POP r6
    POP r5
    POP r4
    RET
```

## Debugging Tips

### Viewing LLVM IR with Debug Info

```bash
clang -S -emit-llvm -g -O0 simple.c -o simple.ll
```

The `-g` flag includes debug information.

### Verbose LLVM Output

```bash
llc -march=cat -debug simple.ll -o simple.s
```

### Viewing DAG (Directed Acyclic Graph)

```bash
llc -march=cat -view-dag-combine1-dags simple.ll
```

This requires Graphviz installed.

### Print Machine Instructions

```bash
llc -march=cat -print-machineinstrs simple.ll -o simple.s
```

## Common Issues and Solutions

### Issue: "Unknown target 'cat'"

**Solution**: Make sure the Cat backend is properly integrated into LLVM and LLVM was rebuilt after integration.

### Issue: "Cannot select" errors

**Solution**: Some LLVM IR operations may not have Cat instruction patterns defined. Check `CatInstrInfo.td` for missing patterns.

### Issue: Register allocation failures

**Solution**: Reduce optimization level or simplify the code. The Cat architecture has limited registers.

### Issue: Stack overflow

**Solution**: Cat VM has limited stack space. Reduce recursion depth or use iteration.

## Advanced Usage

### Custom LLVM Passes

```bash
opt -load=/path/to/pass.so -mypass < simple.ll > optimized.ll
llc -march=cat optimized.ll -o simple.s
```

### Disabling Specific Optimizations

```bash
llc -march=cat -disable-tail-duplicate simple.ll -o simple.s
```

### Stats and Analysis

```bash
llc -march=cat -stats simple.ll -o simple.s
```

## Integration with Cat Assembler

After generating Cat assembly, use the Cat assembler to create the final binary:

```bash
# Using the Cat assembler (adjust path as needed)
catasm simple.s -o simple.bin

# Run on Cat VM
catvm simple.bin
```

## Performance Considerations

1. **Optimization Levels**: Use `-O2` or `-O3` for production code
2. **Inlining**: Small functions benefit from inlining
3. **Loop Unrolling**: May improve performance but increases code size
4. **Register Pressure**: Cat has only 8 general-purpose registers

## Next Steps

1. Experiment with the provided examples
2. Write your own C programs
3. Analyze the generated assembly
4. Optimize for the Cat architecture
5. Contribute improvements to the backend

## Reference

- Cat VM Instruction Set: See `CatVM/Instructions.csv`
- Cat VM Registers: See `CatVM/Registers.csv`
- Cat Assembly Spec: See `CatAssembler/Spec.md`
- LLVM Documentation: https://llvm.org/docs/
