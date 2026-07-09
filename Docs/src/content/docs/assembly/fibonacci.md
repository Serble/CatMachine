---
title: Fibonacci Tutorial
slug: assembly/fibonacci
---

This tutorial prints Fibonacci numbers with the debug print-number interrupt.  
`int 0x90` prints the value currently in `r1`.

```cat
#const INT_PRINT_NUM, 0x90
#const COUNT, 12

start:
    mov r0, 0          ; a = 0
    mov r1, 1          ; b = 1
    mov r4, 0          ; i = 0

loop:
    int INT_PRINT_NUM  ; print current b

    mov r2, r1         ; r2 = b
    add r2, r0         ; r2 = a + b (next)
    mov r0, r1         ; a = old b
    mov r1, r2         ; b = next

    add r4, 1          ; i++
    cmp r4, COUNT
    jne loop

halt:
    jmp halt
```

## How it works

1. `r0` and `r1` store the two latest Fibonacci values (`a` and `b`).
2. Each loop iteration prints `r1` using `int 0x90`.
3. `r2` is a temporary register for `a + b`.
4. The pair shifts forward: `a = b`, `b = a + b`.
5. The loop stops after `COUNT` numbers.

## Build and run (CatLauncher)

Because this tutorial uses the debug print interrupt (`0x90`), you must pass `--test-ints`.

```sh
./CatAssembler fibonacci.cat -o fibonacci.bin
./CatLauncher run --rom fibonacci.bin --test-ints
```

Expected style of output (decimal + hex per line):

```text
1 0x00000001
1 0x00000001
2 0x00000002
3 0x00000003
5 0x00000005
...
```
