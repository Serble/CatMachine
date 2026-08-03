---
title: Interrupts
slug: vm/interrupts
---


Cat systems support CPU interrupts which immediately stop what the CPU is doing and jump the instruction pointer to new code. You may trigger an interrupt with the `int` instruction manually, interrupts are also triggered by errors and user input.

Interrupts `0x00`-`0x0F` are considered error interrupts and will halt the machine by default if not handled. All other interrupts will by default do nothing.

## List

The following interrupts are given meaning by default:

| Interrupt ID | Name                      | Handleable | Description                                                                                                                                |
|--------------|---------------------------|------------|--------------------------------------------------------------------------------------------------------------------------------------------|
| 0x00         | Page Fault                | true       | Memory could not be accessed (out of bounds, or forbidden).                                                                                |
| 0x01         | Invalid Instruction       | true       | Instruction opcode or arguments were invalid                                                                                               |
| 0x02         | Divide by zero            | true       | Tried to divide by zero                                                                                                                    |
| 0x03         | Protection Fault          | true       | A privileged opcode (e.g. `int`, `iret`, `in`, `out`, `setit`) was executed from User mode                                                 |
| 0x04 - 0x0F  | RESERVED                  |            | Reserved for future CPU exceptions                                                                                                         |
| 0x10         | Syscall                   | true       | Raised by the `syscall` instruction. The only way for User mode to enter the kernel directly. The argument convention is up to the kernel. |
| 0x80         | DEPRECATED: Write to sout | false      | Writes the null-terminated string pointed to by `r1` to the host console. This interrupt will be removed soon.                             |
| 0x90         | DEBUG: Print Number       | false      | Prints the number in `r1` followed by `\n`. Only available when `EnableTestingInterrupts` is set on the VM.                                |

As shown above, some interrupts exist as function calls into the system, allowing you to do things like write to the VM's console. You can call them like functions.

The display, disk drives, network cards and similar peripherals are no longer accessed via dedicated interrupts. They are all serial devices now - see the [Serial Protocol](/vm/serial-protocol/) page for how to interact with them.

## Handling

You may handle interrupts by constructing an interrupt handler table and then setting the `it` register (via the privileged `setit` instruction) to a pointer to it. The structure of the table is as follows:

| Offset | Field       | Size (Bytes)  |
|--------|-------------|---------------|
| 0x00   | Entry Count | 1             |
| 0x01   | Entries     | 5 per entry   |

Then each entry is as follows:

| Offset | Field           | Size (Bytes) |
|--------|-----------------|--------------|
| 0x00   | Interrupt Code  | 1            |
| 0x01   | Handler Pointer | 4            |

When a handler is dispatched from User mode the CPU automatically switches `sp` to `ksp`, clears Virtual Mode, and pushes a frame containing the previous `mode`, `mbase`, `mlen`, `sp`, `fl` and `ip`. Handlers must return via the `iret` instruction, which restores that frame; `ret` is reserved for ordinary `call` / `ret` pairs.
