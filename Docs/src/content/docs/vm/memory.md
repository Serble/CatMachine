---
title: Memory
slug: vm/memory
---

Cat systems have a single flat 32 bit byte-addressable memory space. The amount of physical memory is configurable when launching the VM (`--mem <bytes>` in the reference VM, defaulting to 16 MiB). All memory is RAM – there is no separate ROM region after boot; the ROM file is simply copied into the bottom of memory at power-on.

## Power-On State

After a hardware reset the VM is in this state:

| Register / Region | Value                                                 |
|-------------------|-------------------------------------------------------|
| `r0`–`r7`         | `0`                                                   |
| `ip`              | `0`                                                   |
| `sp`              | One past the last byte of physical memory             |
| `fl`              | `0`                                                   |
| `mode`            | `0b00` (Kernel mode)                                  |
| `it`              | `0xFFFFFFFF` (no interrupt table installed yet)       |
| `ksp`             | `0`                                                   |
| `mbase`           | `0`                                                   |
| `mlen`            | size of physical memory                               |
| Memory `[0..N)`   | ROM bytes loaded at offset 0                          |
| Memory `[N..)`    | Zero                                                  |

Because `ip` starts at `0` and the ROM is loaded at `0`, execution begins at the first byte of the ROM file. The stack starts at the very top of memory and grows downward.

## Default Memory Map

There is no enforced map – the bottom of memory simply contains whatever the ROM places there, and the stack grows down from the top. A typical layout used by the firmware and example projects looks like:

```
0x00000000 +-------------------------+
           | ROM (code + read-only   |
           |  data assembled in)     |
           +-------------------------+
           | Heap / mutable globals  |
           |   ...                   |
           |   (grows up)            |
           +-------------------------+
           |        free             |
           +-------------------------+
           |   (grows down)          |
           |   Kernel stack          |
0xMMMMMMMM +-------------------------+   <- initial sp
```

When the kernel launches a user process under [Virtual Mode](Virtual-Mode), it carves out a window inside this same physical memory and points `mbase`/`mlen` at it; the user process then sees that window as its own zero-based address space.

## Stack

The stack is a normal region of memory pointed to by `sp`. It grows **down**: `push` decrements `sp` *before* writing, `pop` reads from `sp` *then* increments. Width-specific opcodes change `sp` by the corresponding number of bytes:

| Opcode          | `sp` delta |
|-----------------|------------|
| `push` / `pop`  | 4          |
| `push16` / `pop16` | 2       |
| `push8` / `pop8`   | 1       |
| `call`          | 4 (pushes the return `ip`) |
| `ret`           | -4 (pops the return `ip`)  |

The kernel maintains a separate kernel stack via [`ksp`](Registers#non-encodable-registers); on every interrupt entry from user mode the CPU automatically swaps `sp` for `ksp` so that handlers run on a clean, kernel-private stack. See the [Virtual Mode](Virtual-Mode) page for details of the saved frame layout.

## Memory Access from Instructions

Any instruction whose argument is annotated `rp` or `ip` in the [Instructions table](Instructions) treats that argument as a memory address rather than a value. In assembly this is written by wrapping the address source in square brackets:

```cat
mov  r1, [r2]      ; load 32 bits from the address in r2
mov  [0x1000], r3  ; store r3 at physical address 0x1000
mov16 [r4], 0xABCD ; store a 16 bit immediate at the address in r4
```

Reads of widths less than 32 bits zero-extend into the destination register. Writes of widths less than 32 bits only modify the low N bytes of the destination address.

The `cpy` instruction performs a block memory copy. The destination address is **always** taken from `r0` regardless of the operand encoding, while the source address and length come from the operands. This is a deliberate convenience for memcpy-style code paths.

## Bounds Checking

All physical memory accesses are bounds-checked against the size of the `Memory` array. Out-of-bounds accesses raise interrupt `0x00` (`PageFault`); the faulting address is pushed onto the stack as a 32 bit pointer before the handler runs.

When Virtual Mode is on, an additional bounds check is performed against `mlen` *before* translation – a user process can never reach memory outside its window even by accident.

The reference VM also has optional debug-mode regions (`--disallow-write` / `--disallow-read`) that fault on access; these only take effect when the VM is built with `DebugMode = true` and are intended purely as a development aid.

## ROM and Reset

The ROM bytes are kept around in their original form. The `0x83` Reset interrupt (and the host pressing a reset button) re-runs the boot sequence: clears the CPU state, optionally wipes memory, and re-copies the ROM into the bottom of memory. This is the only way to recover from a corrupted boot region without restarting the host process.

If the kernel wants to protect itself against accidental ROM overwrites during development, run the VM with `--protect-rom` (debug-mode only) – any write below `Rom.Length` will then raise a page fault.

## Display Buffer

Earlier versions of Cat dedicated a special interrupt to fetch the display buffer base address. The current display is a serial device (port `0x00` on the standard mapping; see [Display](Display) and [Serial Protocol](Serial-Protocol)) and the buffer lives at whatever address the program tells the PPU to use. Common practice is to place it just below the stack so that user code can still grow its heap upward without colliding.
