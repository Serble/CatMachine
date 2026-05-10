#!/bin/sh
# Build and run the LLVM IR tests through the full pipeline:
#   .ll  --CatLLVM-->  .cat  --CatAssembler-->  .bin  --CatVM-->  output
#
# Pass --test-ints so the example programs can use interrupt 0x90 to print
# their result.

set -e
cd "$(dirname "$0")"

ROOT=../..
LLVM_PROJ=$ROOT/CatLLVM/CatLLVM.csproj
ASM_PROJ=$ROOT/CatAssembler/CatAssembler.csproj
VM_PROJ=$ROOT/CatVM/CatVM.csproj

for src in fib.ll array.ll; do
  echo "=== $src ==="
  base="${src%.ll}"
  dotnet run --project "$LLVM_PROJ" -- "$src" -o "$base.cat" >/dev/null
  dotnet run --project "$ASM_PROJ" -- "$base.cat" -o "$base.bin" >/dev/null
  dotnet run --project "$VM_PROJ" -- "$base.bin" --fast --test-ints
  echo
done

# ---- libc test: link test.ll against libc/catvm.ll --------------------------
echo "=== test.ll (linked with libc/catvm.ll) ==="
dotnet run --project "$LLVM_PROJ" -- libc/catvm.ll test.ll -o test.cat >/dev/null
dotnet run --project "$ASM_PROJ" -- test.cat -o test.bin >/dev/null
dotnet run --project "$VM_PROJ" -- test.bin --fast
echo
