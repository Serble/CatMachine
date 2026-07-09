---
title: Virtual Mode
slug: vm/virtual-mode
---

Cat systems support a simple two-tier privilege model with optional address translation. The kernel runs unrestricted with full access to physical memory; user processes run in **Virtual Mode**, which sandboxes them inside a per-process memory window and forbids privileged opcodes.

## Mode Bits

The current privilege state lives in the (non-encodable) `mode` register, an 8 bit value where only the low two bits are defined:

| Bit | Name           | Description                                                  |
|-----|----------------|--------------------------------------------------------------|
| 0   | Virtual Mode   | When set, all memory accesses are translated through `mbase`/`mlen`. |
| 1   | Supervisor     | Reserved for a future driver tier; currently treated as privileged. |

The four combinations form the privilege tiers:

| `mode` | Virt | Supv | Tier       | Notes                                                                  |
|--------|------|------|------------|------------------------------------------------------------------------|
| `0b00` | 0    | 0    | Kernel     | Default at power-on. All opcodes allowed; no address translation.      |
| `0b01` | 1    | 0    | User       | Privileged opcodes raise `ProtectionFault`. Translation active.        |
| `0b10` | 0    | 1    | Supervisor | Reserved – treated as kernel today.                                    |
| `0b11` | 1    | 1    | Driver     | Reserved – the only currently meaningful effect is the `iret` marker.  |

`mode` is never written directly. It changes only via interrupt entry (forced to `0b00`) and `iret` (restored from the interrupt frame).

## Address Translation

When Virtual Mode is on, every guest address `a` used by an instruction is checked and translated:

```
if (a + size > MLen) -> page fault
physical = a + MBase
```

Two registers control translation:

- `mbase` – physical address of the start of the user process's memory window.
- `mlen`  – length of that window in bytes. Acts as the upper bound for guest addresses.

Both registers are set by the kernel only via the interrupt frame restored by `iret`. There are no opcodes to write `mbase`/`mlen` directly. To launch a user process the kernel constructs a fake interrupt frame on the kernel stack containing the desired `mode`, `mbase`, `mlen`, `sp`, `fl` and `ip`, then executes `iret`.

In Kernel mode the translation step is skipped entirely – guest addresses *are* physical addresses (identity mapping). This keeps the kernel and any legacy single-mode programs running with no overhead.

## Privileged Opcodes

The following opcodes raise interrupt `0x03` (`ProtectionFault`) when executed from User mode:

- `int`
- `iret`
- `di`, `ei`
- `in`, `out`
- `setit`, `getit`
- `setksp`, `getksp`

Note that `syscall` is *not* privileged – it is the only way for User-mode code to enter the kernel directly. It unconditionally raises interrupt `0x10`.

## Interrupts and Frame Layout

The interrupt entry path depends on the current privilege tier.

### From Kernel mode (lightweight frame)

The CPU pushes only the return `ip` and a 1-byte marker onto the current stack:

```
[high addr]
    ...
    ip               (4 bytes)   <- pushed first
    marker = 0x00    (1 byte)    <- pushed last
[low addr] <- sp
```

### From User / Driver mode (full frame)

The CPU first switches `sp` to the kernel stack pointer (`ksp`) and clears `mode` to `0b00`, then pushes a complete snapshot of the preempted process onto the kernel stack:

```
[high addr]
    r0, r1, r2, r3, r4, r5, r6, r7   (4 bytes each, in that order – pushed first)
    mlen                              (4 bytes)
    mbase                             (4 bytes)
    fl                                (4 bytes)
    sp (the user sp)                  (4 bytes)
    ip                                (4 bytes)
    marker = 0x01 (user) or 0x02 (driver)   (1 byte – pushed last)
[low addr] <- ksp
```

The handler runs in Kernel mode with full access to physical memory. It must return via `iret`, which pops the marker and:

- `0x00` – pops `ip` only and resumes Kernel mode.
- `0x01` – pops the full frame and restores `mode = 0b01` (User).
- `0x02` – pops the full frame and restores `mode = 0b11` (Driver).
- Any other marker – raises `InvalidInstruction`.

Because the marker selects the restore path, the kernel can launch a user process by hand-rolling a `0x01` frame on top of `ksp` and executing `iret`.

## Booting a User Process — Worked Example

A typical sequence the kernel runs to drop into user code at `userEntry`:

```cat
; one-time setup
setit interruptTable        ; install IT
setksp kernelStackTop       ; install KSP

; build a synthetic user frame on the kernel stack
push 0                      ; r0
push 0                      ; r1
push 0                      ; r2
push 0                      ; r3
push 0                      ; r4
push 0                      ; r5
push 0                      ; r6
push 0                      ; r7
push userMemLen             ; mlen
push userMemBase            ; mbase
push 0                      ; fl
push userMemLen             ; sp = top of user memory (guest address)
push userEntry              ; ip = guest entry point
push8 0x01                  ; marker = user

iret                        ; -> jump into user mode at userEntry
```

After `iret` the CPU is in User mode, `mbase`/`mlen` define the sandbox, the user `ip` is `userEntry`, and the user `sp` points at the top of the user-visible window. Any subsequent interrupt (timer, syscall, page fault, etc.) will reverse this process: switch to `ksp`, clear `mode`, push a `0x01` frame, and jump to the appropriate handler.

## Page Faults

Any access (read, write, or instruction fetch) that violates the `mlen` bound while Virtual Mode is on raises interrupt `0x00` (`PageFault`). The faulting address is pushed onto the stack as a 32 bit pointer before the handler is dispatched. The handler runs in Kernel mode and may resolve the fault and `iret` back, or terminate the offending process.
