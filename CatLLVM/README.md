# CatLLVM

An LLVM-IR → CatVM compiler. Reads a `.ll` text file (e.g. produced by
`clang -S -emit-llvm`) and emits Cat assembly compatible with `CatAssembler`.

```
clang -S -emit-llvm --target=... source.c    # source.ll
dotnet run --project CatLLVM -- source.ll -o source.cat
catasm source.cat -o source.bin
catlaunch run --rom source.bin
```

## Supported subset

CatVM is a 32-bit integer-only machine, so the backend handles:

| LLVM feature | Notes |
|---|---|
| Types | `i1`, `i8`, `i16`, `i32`, `ptr`, `[N x T]`, `void` |
| Globals | integer & array initializers, `c"..."` string blobs, `zeroinitializer` |
| Functions | `define`, `declare`, integer/`ptr` parameters and returns |
| Memory | `alloca`, `load`, `store` |
| Arithmetic | `add`, `sub`, `mul`, `sdiv`, `udiv`, `srem`, `urem` |
| Logic | `and`, `or`, `xor`, `shl`, `lshr`, `ashr` |
| Compare/branch | `icmp` (all 10 predicates), `br`, `br i1` |
| Calls | `call`, `ret`, indirect calls via `ptr` |
| Pointer arithmetic | `getelementptr` (linear / arrays) |
| Casts | `zext`, `sext`, `trunc`, `bitcast`, `ptrtoint`, `inttoptr` |
| SSA | `phi` (resolved by predecessor copy-on-edge) |

Floating point and `i64` are **not** supported (no hardware backing in CatVM).
Structs are not yet supported.

## Calling convention

Matches Catnip so generated code interops with hand-written assembly and
Catnip output:

* `r1`, `r2`, `r3` — first three arguments
* extra args pushed right-to-left by caller (caller cleans up)
* `r0` — return value
* `r4`–`r7` — callee-saved (`r7` is base pointer)
* `r0`–`r3` — caller-saved scratch

## CatVM intrinsics

Declare these in your IR (or via C `extern` declarations) to get direct
access to CatVM features:

| Symbol | C signature | Lowers to |
|---|---|---|
| `__catvm_int` | `void __catvm_int(int8_t num)` | `int <num>` |
| `__catvm_in` | `uint32_t __catvm_in(uint32_t port)` | `in r0, port` |
| `__catvm_out` | `void __catvm_out(uint32_t port, uint32_t v)` | `out port, v` |
| `__catvm_syscall` | `void __catvm_syscall(void)` | `syscall` |
| `__catvm_print` | `void __catvm_print(const char *s)` | `int 0x80` (with `r1 = s`) |
| `__catvm_uptime` | `uint32_t __catvm_uptime(void)` | `int 0x85` (returns `r0`) |

## Code generation strategy

CatLLVM uses **stack-slot codegen**: every SSA value (parameters, instruction
results, allocas) gets its own 4-byte slot relative to `r7`. Each instruction
loads its operands from their slots into `r0`/`r1`, performs the op, and
writes the result back to its slot. This is the same approach `clang -O0`
uses internally — it produces verbose but reliably-correct code with no
register-allocator complexity. Run an LLVM optimizer pass on the IR
beforehand if you care about generated code size.

## How to integrate with C

Compile your C with clang's "generic" target then post-process the IR. CatVM
isn't a registered LLVM target so you need to keep clang in `--target=` mode
that produces 32-bit IR without machine-specific intrinsics:

```
clang -S -emit-llvm -O0 -m32 -ffreestanding -nostdlib \
      -target i386-unknown-none source.c -o source.ll
```

Then run CatLLVM on `source.ll`. (The frontend's job is to give us clean
IR; the backend's job is to lower it.)

You can also pass several `.ll` files to a single invocation; later files
override earlier declarations, which acts like a tiny linker:

```
dotnet run --project CatLLVM -- libc/catvm.ll source.ll -o source.cat
```

## CatVM-libc

A small freestanding C standard library lives in
`ExampleProjects/LlvmTest/libc/`. It currently provides:

| Function | Description |
|---|---|
| `puts_raw` / `puts` / `putchar` | string / char output via `int 0x80` |
| `puti` / `putu` / `putx` | print integer as signed-decimal / unsigned-decimal / lowercase-hex |
| `uptime_ms` | wall-clock ms since VM start |
| `exit` / `halt` | shutdown (`int 0x82`) and pause (`int 0x81`) |
| `memset` / `memcpy` / `strlen` / `strcmp` | the obvious things |

`catvm.h` is the header to include from C; `catvm.ll` is the hand-translated
IR you link against today (until a real `clang --target=catvm` exists).
`ExampleProjects/LlvmTest/test.c` + `test.ll` exercise the full surface and
are run by `run.sh`.
