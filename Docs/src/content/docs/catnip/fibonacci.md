---
title: Fibonacci Tutorial
slug: catnip/fibonacci
---

This is the Catnip equivalent of the Assembly Fibonacci tutorial.  
It prints Fibonacci values using the std library `debug_num()` helper (which uses `int 0x90` internally).

```nip
#include "std"

#define COUNT, 12

main();

fun main() {
    let a:4 = 0;
    let b:4 = 1;
    let i:4 = 0;

    while (i:4 < ${COUNT}) {
        debug_num(b:4);

        let next:4 = a:4 + b:4;
        a:4 = b:4;
        b:4 = next:4;
        i:4 = i:4 + 1;
    }

    while (1) { }
}
```

## How it works

1. `a` and `b` track consecutive Fibonacci numbers.
2. `debug_num(b:4)` calls the std helper that prints via debug interrupt `0x90`.
3. `next = a + b`, then the pair advances (`a = b`, `b = next`).
4. The loop stops after `COUNT` values.

## Build and run (Catnip + CatLauncher)

Because this tutorial uses debug interrupt `0x90`, you must pass `--test-ints`.

```sh
./Catnip.Compiler fibonacci.nip -o fibonacci.bin
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
