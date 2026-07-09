---
title: Registers
slug: vm/registers
---

There are 8 general purpose registers (`r0`–`r7`) plus three additional registers that may be referenced in encoded instructions: the stack pointer (`sp`), the instruction pointer (`ip`) and the flags register (`fl`). On top of these, the CPU has a number of internal "non-encodable" registers used for interrupts and virtual mode that are only accessible through dedicated opcodes.

## Encodable Registers

These are the only registers whose ID may appear directly inside an encoded instruction. Register IDs are 1 byte; valid encoded IDs are `0x00`–`0x0a`. Any other ID raises an `InvalidInstruction` interrupt.

| Name | ID | Description                                |
|------|----|--------------------------------------------|
| r0   | 0  | Return value / `cpy` destination address   |
| r1   | 1  | Arg 1                                      |
| r2   | 2  | Arg 2                                      |
| r3   | 3  | Arg 3                                      |
| r4   | 4  | Callee preserved                           |
| r5   | 5  | Callee preserved                           |
| r6   | 6  | Callee preserved                           |
| r7   | 7  | Callee preserved                           |
| sp   | 8  | Stack pointer                              |
| ip   | 9  | Instruction pointer                        |
| fl   | a  | Flags register (see below)                 |

The argument / preserved register split is a calling convention used by the assembler, the Catnip compiler and the Cat C ABI – the hardware itself does not enforce it.

## Flags Register (`fl`)

`fl` is a 32 bit register. Only the low 4 bits are currently defined; the remaining bits are reserved and should be left as zero.

| Bit | Name           | Set By              | Meaning                                                              |
|-----|----------------|---------------------|----------------------------------------------------------------------|
| 0   | Zero (`Z`)     | `cmp`, conditionals | Result was zero / operands were equal                                |
| 1   | Carry (`C`)    | `cmp`               | Unsigned underflow (left operand was less than right operand)        |
| 2   | Sign (`S`)     | `cmp`               | Result, interpreted as signed, is negative                           |
| 3   | Overflow (`O`) | `cmp`               | Signed overflow occurred                                             |

Conditional jumps consult these bits; for example `jul` (unsigned `<`) jumps when `C = 1`, and `jig` (signed `>`) jumps when `Z = 0` and `S = O`.

## Non-Encodable Registers

These registers cannot be referenced by ID in an instruction. They are accessed via dedicated opcodes (or implicitly by the hardware on interrupt entry / `iret`).

| Name    | Width  | Description                                                                                       | Access                                              |
|---------|--------|---------------------------------------------------------------------------------------------------|-----------------------------------------------------|
| `mode`  | 8 bit  | Mode bits. Bit 0 = Virtual Mode, bit 1 = Supervisor Mode. The combination selects the privilege tier (Kernel / User / Supervisor / Driver). | Updated implicitly on interrupt entry and `iret`. |
| `it`    | 32 bit | Interrupt Table pointer. Address of the 256-entry interrupt vector table.                         | `setit` / `getit` (privileged)                      |
| `ksp`   | 32 bit | Kernel Stack Pointer. Hardware reloads `sp` from `ksp` on every user→kernel interrupt entry.       | `setksp` / `getksp` (privileged)                    |
| `mbase` | 32 bit | Virtual Mode base address. All guest addresses are offset by `mbase` while Virtual Mode is on.     | Set by the kernel via the interrupt frame on `iret` |
| `mlen`  | 32 bit | Virtual Mode length. Guest addresses are bounds-checked against `mlen` while Virtual Mode is on.   | Set by the kernel via the interrupt frame on `iret` |

### Privilege Tiers

`mode` encodes the current privilege tier:

| `mode` | Virt | Supv | Tier       | Notes                                                                   |
|--------|------|------|------------|-------------------------------------------------------------------------|
| `0b00` | 0    | 0    | Kernel     | Full access, no virtual translation.                                    |
| `0b01` | 1    | 0    | User       | Privileged opcodes raise `ProtectionFault`. Virtual translation active. |
| `0b10` | 0    | 1    | Supervisor | Reserved for a future driver tier; treated as privileged.               |
| `0b11` | 1    | 1    | Driver     | Reserved for a future driver tier.                                      |

Privileged opcodes (`int`, `di`, `ei`, `in`, `out`, `setit`, `getit`, `setksp`, `getksp`, `iret`) raise interrupt `0x03` (`ProtectionFault`) when executed from User mode.
