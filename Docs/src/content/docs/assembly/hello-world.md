---
title: Hello World Tutorial
---

# Hello World in Cat Assembly (Explained)

This is the smallest useful Cat Assembly program: load a pointer to a null-terminated string into `r1`, then call interrupt `0x80` to print it.

```cat
#const INT_PRINT, 0x80

start:
    mov r1, hello_msg    ; r1 = pointer to C-style string
    int INT_PRINT        ; VM prints string at address in r1

halt:
    jmp halt             ; keep CPU alive (simple infinite loop)

hello_msg:
    dstr "Hello, World!\n\0"
```

## How it works

1. `#const INT_PRINT, 0x80` gives the print interrupt a readable name.
2. `mov r1, hello_msg` places the assembled address of `hello_msg` into `r1`.
3. `int INT_PRINT` invokes the VM stdout handler, which reads bytes from `r1` until `\0`.
4. `dstr "Hello, World!\n\0"` emits the message bytes, including newline (`\n`) and terminator (`\0`).
5. `jmp halt` prevents execution from falling through into string data.

## Build and run (CatLauncher)

```sh
./CatAssembler hello.cat -o hello.bin
./CatLauncher run --rom hello.bin
```

Expected output:

```text
Hello, World!
```
