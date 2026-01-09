# Quick Start: Compiling Your First Program with Cat Backend

This is a step-by-step guide to compile your first C program to Cat VM assembly using the LLVM backend.

## Prerequisites

Complete the build process first:
```bash
cd llvm-backend
./build-llvm-cat.sh
source ~/llvm-cat/setup-env.sh
```

## Your First Program

### Step 1: Create a Simple C Program

```bash
cat > hello_cat.c << 'EOF'
// hello_cat.c - Your first Cat VM program
int add(int a, int b) {
    return a + b;
}

int main() {
    int result = add(10, 32);
    return result;  // Should return 42
}
EOF
```

### Step 2: Compile to LLVM IR

```bash
clang -S -emit-llvm -O2 hello_cat.c -o hello_cat.ll
```

**What this does:** Converts your C code to LLVM Intermediate Representation, a platform-independent format.

**Output:** `hello_cat.ll` (LLVM IR text file)

### Step 3: Compile to Cat Assembly

```bash
llc -march=cat hello_cat.ll -o hello_cat.s
```

**What this does:** Uses the Cat backend to convert LLVM IR to Cat VM assembly.

**Output:** `hello_cat.s` (Cat assembly file)

### Step 4: View the Assembly

```bash
cat hello_cat.s
```

You should see Cat assembly code with instructions like:
- `PUSH r4, r5, r6, r7` - Save registers
- `MOV r0, r1` - Move data
- `ADD r1, r2` - Add values
- `RET` - Return from function

## Understanding the Output

Let's break down what the compiler generated:

### Function Prologue
```asm
add:
    PUSH r4
    PUSH r5
    PUSH r6
    PUSH r7
    SUB sp, 0
    MOV r7, sp
```
This saves callee-saved registers and sets up the stack frame.

### Function Body
```asm
    ADD r1, r2    ; Add arguments (r1 = a, r2 = b)
    MOV r0, r1    ; Move result to return register
```
This performs the actual computation.

### Function Epilogue
```asm
    MOV sp, r7
    ADD sp, 0
    POP r7
    POP r6
    POP r5
    POP r4
    RET
```
This restores registers and returns.

## More Examples

### Example 2: Conditional Logic

```bash
cat > conditional.c << 'EOF'
int max(int a, int b) {
    if (a > b) {
        return a;
    }
    return b;
}

int main() {
    return max(15, 20);
}
EOF

clang -S -emit-llvm -O2 conditional.c -o conditional.ll
llc -march=cat conditional.ll -o conditional.s
cat conditional.s
```

Look for:
- `CMP` - Compare instruction
- `JUG`, `JUGE`, etc. - Conditional jumps

### Example 3: Loop

```bash
cat > loop.c << 'EOF'
int sum_to_n(int n) {
    int sum = 0;
    int i = 1;
    while (i <= n) {
        sum = sum + i;
        i = i + 1;
    }
    return sum;
}

int main() {
    return sum_to_n(10);  // Returns 55
}
EOF

clang -S -emit-llvm -O2 loop.c -o loop.ll
llc -march=cat loop.ll -o loop.s
cat loop.s
```

Look for:
- Loop labels (`.L_loop:`)
- `JMP` - Unconditional jump (loop back)
- Loop increment and comparison

### Example 4: Function Calls

```bash
cat > calls.c << 'EOF'
int helper(int x) {
    return x * 2;
}

int main() {
    int a = helper(10);
    int b = helper(20);
    return a + b;  // Returns 60
}
EOF

clang -S -emit-llvm -O2 calls.c -o calls.ll
llc -march=cat calls.ll -o calls.s
cat calls.s
```

Look for:
- `CALL 0xFF, helper` - Function call
- Argument passing in `r1, r2, r3`
- Return value in `r0`

## Optimization Levels

Try different optimization levels to see the impact:

### No Optimization (-O0)
```bash
clang -S -emit-llvm -O0 hello_cat.c -o hello_cat_O0.ll
llc -march=cat hello_cat_O0.ll -o hello_cat_O0.s
wc -l hello_cat_O0.s
```

**Result:** Larger, more readable code with all operations explicit.

### Moderate Optimization (-O2)
```bash
clang -S -emit-llvm -O2 hello_cat.c -o hello_cat_O2.ll
llc -march=cat hello_cat_O2.ll -o hello_cat_O2.s
wc -l hello_cat_O2.s
```

**Result:** Balanced optimization, smaller code, still readable.

### Aggressive Optimization (-O3)
```bash
clang -S -emit-llvm -O3 hello_cat.c -o hello_cat_O3.ll
llc -march=cat hello_cat_O3.ll -o hello_cat_O3.s
wc -l hello_cat_O3.s
```

**Result:** Maximum optimization, may inline functions, smallest code.

### Size Optimization (-Os)
```bash
clang -S -emit-llvm -Os hello_cat.c -o hello_cat_Os.ll
llc -march=cat hello_cat_Os.ll -o hello_cat_Os.s
wc -l hello_cat_Os.s
```

**Result:** Optimized for code size, good for embedded systems.

## Common Patterns

### Pattern 1: Register Usage
```
r0 - Return value
r1 - First argument
r2 - Second argument
r3 - Third argument
r4-r7 - Preserved across calls
sp - Stack pointer
```

### Pattern 2: Calling Convention
```asm
; Caller
MOV r1, 10        ; First argument
MOV r2, 20        ; Second argument
CALL 0xFF, func
; r0 now has return value

; Callee
func:
    ; r1 and r2 have arguments
    ADD r1, r2
    MOV r0, r1    ; Set return value
    RET
```

### Pattern 3: Stack Frame
```asm
function:
    ; Prologue
    PUSH r4, r5, r6, r7
    SUB sp, <frame_size>
    MOV r7, sp
    
    ; Body
    ; ...
    
    ; Epilogue
    MOV sp, r7
    ADD sp, <frame_size>
    POP r7, r6, r5, r4
    RET
```

## One-Line Commands

For quick testing:

```bash
# Compile in one command
echo 'int main() { return 42; }' | clang -x c -S -emit-llvm - -o - | llc -march=cat -o -

# Compile with optimization
echo 'int f(int x) { return x*2; }' | clang -x c -S -emit-llvm -O2 - -o - | llc -march=cat -o -

# Count instructions
clang -S -emit-llvm -O2 program.c -o - | llc -march=cat -o - | grep -E "^\s+(MOV|ADD|SUB)" | wc -l
```

## Makefile Example

Create a `Makefile` for automatic compilation:

```makefile
CC = clang
LLC = llc
CFLAGS = -S -emit-llvm -O2
LLCFLAGS = -march=cat

SOURCES = $(wildcard *.c)
LLVM_IR = $(SOURCES:.c=.ll)
ASM = $(SOURCES:.c=.s)

.PHONY: all clean

all: $(ASM)

%.ll: %.c
	$(CC) $(CFLAGS) $< -o $@

%.s: %.ll
	$(LLC) $(LLCFLAGS) $< -o $@

clean:
	rm -f $(LLVM_IR) $(ASM)

# Run specific target
run-%: %.s
	@echo "Generated assembly for $*:"
	@cat $<
```

Usage:
```bash
# Compile all .c files
make

# Compile specific file
make hello_cat.s

# Clean up
make clean

# Show output
make run-hello_cat
```

## Debugging Tips

### View LLVM IR
```bash
clang -S -emit-llvm -O2 program.c -o program.ll
cat program.ll
```
Helps understand what LLVM sees before Cat backend.

### Verbose LLC Output
```bash
llc -march=cat -debug program.ll -o program.s 2>&1 | less
```
Shows detailed information about compilation.

### Compare Optimization Levels
```bash
for opt in O0 O1 O2 O3 Os; do
    clang -S -emit-llvm -$opt program.c -o program_$opt.ll
    llc -march=cat program_$opt.ll -o program_$opt.s
    echo "$opt: $(wc -l < program_$opt.s) lines"
done
```

## Next Steps

1. **Try the examples in `examples/` directory**
   ```bash
   cd llvm-backend/examples
   clang -S -emit-llvm -O2 fibonacci.c -o fibonacci.ll
   llc -march=cat fibonacci.ll -o fibonacci.s
   ```

2. **Write your own programs**
   - Start with simple arithmetic
   - Add conditionals
   - Try loops
   - Experiment with recursion

3. **Run the full test suite**
   ```bash
   cd llvm-backend
   ./test-cat-backend.sh
   ```

4. **Read the documentation**
   - [USAGE.md](USAGE.md) - Detailed usage
   - [COMPLETE_EXAMPLE.md](COMPLETE_EXAMPLE.md) - Full workflow
   - [INDEX.md](INDEX.md) - Navigation

## Summary

To compile any C program:

```bash
# Step 1: Setup (once per terminal session)
source ~/llvm-cat/setup-env.sh

# Step 2: Compile
clang -S -emit-llvm -O2 your_program.c -o your_program.ll
llc -march=cat your_program.ll -o your_program.s

# Step 3: View result
cat your_program.s
```

That's it! You're now compiling C to Cat VM assembly with LLVM! 🎉
