---
title: Hello World Tutorial
slug: catnip/hello-world
---

# Hello World in Catnip (Explained)

This is the Catnip equivalent of the Assembly hello world tutorial.  
It uses the Catnip std library `print()` helper.

```nip
#include "std"

main();

fun main() {
    let msg:4 = "Hello, World!\n\0";
    print(msg:4);

    while (1) { }
}
```

## How it works

1. `#include "std"` imports std helpers, including `print`.
2. `let msg:4 = "Hello, World!\n\0";` stores a pointer to null-terminated string data.
3. `print(msg:4);` calls std library printing (which uses interrupt `0x80` internally).
4. `while (1) { }` keeps execution from running into unrelated memory.

## Build and run (Catnip + CatLauncher)

```sh
./Catnip.Compiler hello-world.nip -o hello-world.bin
./CatLauncher run --rom hello-world.bin
```

Expected output:

```text
Hello, World!
```
