---
title: Interrupts
slug: vm/interrupts
---


Cat systems support CPU interrupts which immediately stop what the CPU is doing and jump the instruction pointer to new code. You may trigger an interrupt with the `int` instruction manually, interrupts are also triggered by errors and user input.

Interrupts `0x00`-`0x0F` are considered error interrupts and will halt the machine by default if not handled. All other interrupts will by default do nothing.

## List

The following interrupts are given meaning by default:

| Interrupt ID | Name                    | Handleable | Description                                                                 |
|--------------|-------------------------|------------|-----------------------------------------------------------------------------|
| 0x00         | Page Fault              | true       | Memory could not be accessed (out of bounds, or forbidden). The faulting address is pushed onto the stack as a 32 bit pointer. |
| 0x01         | Invalid Instruction     | true       | Instruction opcode or arguments were invalid                                |
| 0x02         | Divide by zero          | true       | Tried to divide by zero                                                     |
| 0x03         | Protection Fault        | true       | A privileged opcode (e.g. `int`, `iret`, `in`, `out`, `setit`) was executed from User mode |
| 0x04 - 0x0F  | RESERVED                |            | Reserved for future CPU exceptions                                          |
| 0x10         | Syscall                 | true       | Raised by the `syscall` instruction. The only way for User mode to enter the kernel directly. The argument convention is up to the kernel. |
| 0x70         | Receive Input           | true       | Receives input from an input device (e.g. a keyboard). `r1` is the device ID, `r2` is the input type (for keyboards: `0` = key down, `1` = key up), and `r3` is the value (for keyboards, the keycode — literally `'A'` for the A key). |
| 0x71         | Hardware Timer Callback | true       | Called by a hardware timer when it completes. `r1` holds the timer ID.       |
| 0x72         | Disk Operation Finish   | true       | Raised by a disk drive when an asynchronous read/write completes. `r1` holds the disk ID. |
| 0x73         | NIC Notification        | true       | Raised by a virtual network card when a packet has been received. `r1` holds the NIC ID. |
| 0x80         | Write to sout           | false      | Writes the null-terminated string pointed to by `r1` to the host console.    |
| 0x81         | Halt                    | false      | Pause execution permanently                                                 |
| 0x82         | Shutdown                | false      | Poweroff                                                                    |
| 0x83         | Reset                   | false      | Restart (ip = 0)                                                            |
| 0x85         | Get Uptime MSEC         | false      | Returns the uptime in milliseconds in `r0`                                  |
| 0x90         | DEBUG: Print Number     | false      | Prints the number in `r1` followed by `\n`. Only available when `EnableTestingInterrupts` is set on the VM. |

As shown above, some interrupts exist as function calls into the system, allowing you to do things like write to the VM's console. You can call them like functions.

The display, disk drives, network cards and similar peripherals are no longer accessed via dedicated interrupts. They are all serial devices now — see the [Display](Display) and [Serial Protocol](Serial-Protocol) pages for how to interact with them.

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
