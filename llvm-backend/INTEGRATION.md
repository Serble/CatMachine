# Quick Start: Integrating Cat Backend with LLVM

This guide provides step-by-step instructions for integrating the Cat backend into an existing LLVM installation.

## Prerequisites

- LLVM source code (version 13.0 or later recommended)
- CMake 3.13.4 or later
- C++ compiler with C++14 support
- Ninja or Make build system

## Step 1: Copy Backend Files

```bash
# Navigate to LLVM source directory
cd /path/to/llvm-project/llvm

# Copy the Cat backend
cp -r /path/to/CatMachine/llvm-backend/Cat lib/Target/Cat
```

## Step 2: Register the Target

### Edit `lib/Target/LLVMBuild.txt`

Add `Cat` to the subdirectories list:

```ini
[common]
subdirectories = 
    AArch64
    AMDGPU
    ARM
    ...
    Cat     # <-- Add this line
    ...
```

### Edit `lib/Target/CMakeLists.txt`

Add the Cat subdirectory:

```cmake
add_subdirectory(AArch64)
add_subdirectory(AMDGPU)
add_subdirectory(ARM)
# ... other targets ...
add_subdirectory(Cat)   # <-- Add this line
```

## Step 3: Configure LLVM Build

```bash
# Create build directory
mkdir build
cd build

# Configure with CMake (including Cat target)
cmake -G Ninja ../llvm \
    -DLLVM_TARGETS_TO_BUILD="X86" \
    -DLLVM_EXPERIMENTAL_TARGETS_TO_BUILD="Cat" \
    -DCMAKE_BUILD_TYPE=Release \
    -DLLVM_ENABLE_PROJECTS="clang" \
    -DCMAKE_INSTALL_PREFIX=/usr/local/llvm-cat

# Alternative: Build all default targets plus Cat
cmake -G Ninja ../llvm \
    -DLLVM_EXPERIMENTAL_TARGETS_TO_BUILD="Cat" \
    -DCMAKE_BUILD_TYPE=Release \
    -DLLVM_ENABLE_PROJECTS="clang"
```

## Step 4: Build LLVM

```bash
# Build (this will take a while)
ninja

# Or with make:
# make -j$(nproc)

# Optional: Run tests
ninja check-llvm

# Install (optional)
ninja install
```

## Step 5: Verify Installation

```bash
# Check if Cat target is available
./bin/llc --version | grep Cat

# Should output something like:
# Registered Targets:
#   ...
#   cat      - Cat VM
#   ...

# Test with a simple example
echo 'int main() { return 42; }' > test.c
./bin/clang -S -emit-llvm test.c -o test.ll
./bin/llc -march=cat test.ll -o test.s
cat test.s
```

## Step 6: Set Up Environment

Add to your `.bashrc` or `.zshrc`:

```bash
export PATH=/usr/local/llvm-cat/bin:$PATH
export LLVM_CAT_HOME=/usr/local/llvm-cat
```

## Minimal Build (Cat Target Only)

If you only want to build the Cat target for testing:

```bash
cmake -G Ninja ../llvm \
    -DLLVM_TARGETS_TO_BUILD="X86" \
    -DLLVM_EXPERIMENTAL_TARGETS_TO_BUILD="Cat" \
    -DCMAKE_BUILD_TYPE=Debug \
    -DLLVM_OPTIMIZED_TABLEGEN=ON \
    -DLLVM_BUILD_TOOLS=OFF \
    -DLLVM_BUILD_UTILS=OFF

ninja llc
```

This builds only `llc` with Cat support, which is much faster.

## Docker-based Build (Isolated Environment)

Create a `Dockerfile`:

```dockerfile
FROM ubuntu:22.04

RUN apt-get update && apt-get install -y \
    build-essential \
    cmake \
    ninja-build \
    python3 \
    git

WORKDIR /workspace

# Clone LLVM
RUN git clone --depth=1 --branch=release/15.x https://github.com/llvm/llvm-project.git

# Copy Cat backend
COPY llvm-backend/Cat /workspace/llvm-project/llvm/lib/Target/Cat

# Patch LLVM build files
RUN echo 'add_subdirectory(Cat)' >> /workspace/llvm-project/llvm/lib/Target/CMakeLists.txt

# Build
WORKDIR /workspace/build
RUN cmake -G Ninja ../llvm-project/llvm \
    -DLLVM_TARGETS_TO_BUILD="X86" \
    -DLLVM_EXPERIMENTAL_TARGETS_TO_BUILD="Cat" \
    -DCMAKE_BUILD_TYPE=Release && \
    ninja llc

CMD ["/bin/bash"]
```

Build and run:

```bash
docker build -t llvm-cat .
docker run -it -v $(pwd):/work llvm-cat
```

## Troubleshooting

### TableGen Errors

If you see errors about missing TableGen includes:

1. Ensure all `.td` files are in `lib/Target/Cat`
2. Check `CMakeLists.txt` has all `tablegen()` commands
3. Rebuild from clean: `ninja clean && ninja`

### Missing Symbols

If you get linker errors about undefined symbols:

1. Check all `.cpp` files are listed in `CMakeLists.txt`
2. Ensure subdirectories (TargetInfo, MCTargetDesc, InstPrinter) are added
3. Verify include paths are correct

### "Cannot select" Errors

If `llc` produces "Cannot select" errors:

1. Some IR operations need patterns in `CatInstrInfo.td`
2. Try with `-O0` first
3. Check TableGen output: `ninja CatCommonTableGen`

## Verification Tests

After building, run these tests:

```bash
# Test 1: Simple arithmetic
echo 'int add(int a, int b) { return a + b; }' > test1.c
clang -S -emit-llvm -O0 test1.c && llc -march=cat test1.ll

# Test 2: Control flow
cat > test2.c << 'EOF'
int max(int a, int b) {
    if (a > b) return a;
    return b;
}
EOF
clang -S -emit-llvm -O2 test2.c && llc -march=cat test2.ll

# Test 3: Function calls
cat > test3.c << 'EOF'
int helper(int x) { return x * 2; }
int main() { return helper(21); }
EOF
clang -S -emit-llvm -O2 test3.c && llc -march=cat test3.ll
```

All three should compile without errors.

## Alternative: Standalone Backend

For development, you can build the backend standalone without full LLVM:

```bash
# This requires LLVM TableGen installed
mkdir standalone-build && cd standalone-build
cmake ../llvm-backend/Cat \
    -DLLVM_DIR=/usr/local/lib/cmake/llvm \
    -DCMAKE_BUILD_TYPE=Debug
make
```

## Next Steps

1. Read `USAGE.md` for compilation examples
2. Check `examples/` directory for sample programs
3. Experiment with optimization levels
4. Contribute improvements!

## Getting Help

- LLVM Discourse: https://discourse.llvm.org/
- LLVM IRC: #llvm on OFTC
- Cat VM Issues: [Repository URL]

## References

- LLVM Documentation: https://llvm.org/docs/
- Writing an LLVM Backend: https://llvm.org/docs/WritingAnLLVMBackend.html
- TableGen: https://llvm.org/docs/TableGen/
