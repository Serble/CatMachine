---
title: Instructions
slug: vm/instructions
---

Cat instructions are variably lengthed – the size of an encoded instruction depends on its opcode and the kinds of arguments it takes. This keeps assembled programs compact.

No instruction has more than 2 arguments. An argument may be one of the following:

- `r` – A register, encoded as a single byte (see IDs on the [Registers page](Registers)).
- `i` or `i32` – A 32 bit value, little-endian, 4 bytes.
- `i16` – A 16 bit value, little-endian, 2 bytes.
- `i8` – An 8 bit value, single byte.

Pointer / memory-address forms reuse the same encodings, but the value is interpreted as an address into guest memory:

- `rp` – A register containing the target memory address. Encoded the same as `r`.
- `ip` – A 32 bit immediate memory address. Encoded the same as `i32`.

For `jmp`-style instructions the register operand has a special encoding: passing register ID `0xFF` (which is not a valid encodable register) means "no base", causing the immediate to be used as an absolute address. Otherwise the destination is `register + immediate`.

If the executor reads an opcode that is not in the table below, it raises interrupt `0x01` (`InvalidInstruction`). Privileged opcodes (marked **Yes** in the *Privileged* column) raise interrupt `0x03` (`ProtectionFault`) when executed from User mode – see the [Registers page](Registers) for the privilege model.

| OP Code | Instruction | Args    | Description                                                                                                          | Cycles | Privileged |
|---------|-------------|---------|----------------------------------------------------------------------------------------------------------------------|--------|------------|
| 00      | MOV / MOV32 | r, r    | Copy a 32 bit value between registers.                                                                               | 2      | No         |
| 01      | MOV / MOV32 | r, i    | Load a 32 bit immediate into a register.                                                                             | 2      | No         |
| 02      | MOV / MOV32 | r, rp   | Load a 32 bit value from `[rp]` into a register.                                                                     | 6      | No         |
| 03      | MOV / MOV32 | r, ip   | Load a 32 bit value from `[ip]` into a register.                                                                     | 5      | No         |
| 04      | MOV / MOV32 | rp, r   | Store a 32 bit register value at `[rp]`.                                                                             | 8      | No         |
| 05      | MOV / MOV32 | rp, i   | Store a 32 bit immediate at `[rp]`.                                                                                  | 7      | No         |
| 06      | MOV / MOV32 | ip, r   | Store a 32 bit register value at `[ip]`.                                                                             | 7      | No         |
| 07      | MOV / MOV32 | ip, i   | Store a 32 bit immediate at `[ip]`.                                                                                  | 6      | No         |
| 08      | MOV16       | r, rp   | Load a 16 bit value from `[rp]` (zero extended).                                                                     | 6      | No         |
| 09      | MOV16       | r, ip   | Load a 16 bit value from `[ip]` (zero extended).                                                                     | 5      | No         |
| 0a      | MOV16       | rp, r   | Store the low 16 bits of a register at `[rp]`.                                                                       | 8      | No         |
| 0b      | MOV16       | rp, i16 | Store a 16 bit immediate at `[rp]`.                                                                                  | 7      | No         |
| 0c      | MOV16       | ip, r   | Store the low 16 bits of a register at `[ip]`.                                                                       | 7      | No         |
| 0d      | MOV16       | ip, i16 | Store a 16 bit immediate at `[ip]`.                                                                                  | 6      | No         |
| 0e      | MOV8        | r, rp   | Load an 8 bit value from `[rp]` (zero extended).                                                                     | 6      | No         |
| 0f      | MOV8        | r, ip   | Load an 8 bit value from `[ip]` (zero extended).                                                                     | 5      | No         |
| 10      | MOV8        | rp, r   | Store the low 8 bits of a register at `[rp]`.                                                                        | 8      | No         |
| 11      | MOV8        | rp, i8  | Store an 8 bit immediate at `[rp]`.                                                                                  | 7      | No         |
| 12      | MOV8        | ip, r   | Store the low 8 bits of a register at `[ip]`.                                                                        | 7      | No         |
| 13      | MOV8        | ip, i8  | Store an 8 bit immediate at `[ip]`.                                                                                  | 6      | No         |
| 14      | ADD         | r, r    | `arg1 = arg1 + arg2`.                                                                                                | 2      | No         |
| 15      | ADD         | r, i    | `arg1 = arg1 + imm`.                                                                                                 | 2      | No         |
| 16      | SUB         | r, r    | `arg1 = arg1 - arg2`.                                                                                                | 2      | No         |
| 17      | SUB         | r, i    | `arg1 = arg1 - imm`.                                                                                                 | 2      | No         |
| 18      | UMUL        | r, r    | Unsigned multiply, low 32 bits stored in `arg1`.                                                                     | 8      | No         |
| 19      | UMUL        | r, i    | Unsigned multiply by immediate.                                                                                      | 8      | No         |
| 1a      | IMUL        | r, r    | Signed multiply, low 32 bits stored in `arg1`.                                                                       | 8      | No         |
| 1b      | IMUL        | r, i    | Signed multiply by immediate.                                                                                        | 8      | No         |
| 1c      | UDIV        | r, r    | Unsigned divide. `arg1` becomes the quotient (floor) and `arg2` becomes the remainder. A zero divisor returns 0/0.   | 32     | No         |
| 1d      | IDIV        | r, r    | Signed divide. `arg1` becomes the quotient (floor) and `arg2` becomes the remainder. A zero divisor returns 0/0.     | 32     | No         |
| 1e      | INT         | r       | Trigger interrupt number `r & 0xFF`.                                                                                 | 64     | Yes        |
| 1f      | INT         | i8      | Trigger interrupt number `imm`.                                                                                      | 64     | Yes        |
| 20      | PUSH / PUSH32 | r     | Push a 32 bit register value onto the stack.                                                                         | 6      | No         |
| 21      | PUSH / PUSH32 | i     | Push a 32 bit immediate onto the stack.                                                                              | 6      | No         |
| 22      | PUSH16      | r       | Push the low 16 bits of a register onto the stack.                                                                   | 6      | No         |
| 23      | PUSH16      | i16     | Push a 16 bit immediate onto the stack.                                                                              | 6      | No         |
| 24      | PUSH8       | r       | Push the low 8 bits of a register onto the stack.                                                                    | 6      | No         |
| 25      | PUSH8       | i8      | Push an 8 bit immediate onto the stack.                                                                              | 6      | No         |
| 26      | POP / POP32 | r       | Pop a 32 bit value into a register.                                                                                  | 4      | No         |
| 27      | POP16       | r       | Pop a 16 bit value into a register (zero extended).                                                                  | 4      | No         |
| 28      | POP8        | r       | Pop an 8 bit value into a register (zero extended).                                                                  | 4      | No         |
| 29      | OR          | r, r    | `arg1 = arg1 \| arg2`.                                                                                               | 3      | No         |
| 2a      | OR          | r, i    | `arg1 = arg1 \| imm`.                                                                                                | 3      | No         |
| 2b      | AND         | r, r    | `arg1 = arg1 & arg2`.                                                                                                | 3      | No         |
| 2c      | AND         | r, i    | `arg1 = arg1 & imm`.                                                                                                 | 3      | No         |
| 2d      | XOR         | r, r    | `arg1 = arg1 ^ arg2`.                                                                                                | 3      | No         |
| 2e      | XOR         | r, i    | `arg1 = arg1 ^ imm`.                                                                                                 | 3      | No         |
| 2f      | NOT         | r       | `arg1 = ~arg1`.                                                                                                      | 2      | No         |
| 30      | JMP         | r, i    | Set `ip = r + i`. If `r == 0xFF`, set `ip = i`.                                                                      | 2      | No         |
| 31      | CMP         | r, r    | Set flags from `arg1 - arg2`.                                                                                        | 2      | No         |
| 32      | CMP         | r, i    | Set flags from `arg1 - imm`.                                                                                         | 2      | No         |
| 33      | CMP         | i, r    | Set flags from `imm - arg2`.                                                                                         | 2      | No         |
| 34      | CMP         | i, i    | Set flags from `imm1 - imm2` (silly, but assembled out of constant folding).                                         | 2      | No         |
| 35      | JZ / JE     | r, i    | Jump if `Z = 1` (also: operands were equal).                                                                         | 3      | No         |
| 36      | JNZ / JNE   | r, i    | Jump if `Z = 0`.                                                                                                     | 3      | No         |
| 37      | JUL         | r, i    | Unsigned `<`. Jump if `C = 1`.                                                                                       | 3      | No         |
| 38      | JULE        | r, i    | Unsigned `<=`. Jump if `C = 1` or `Z = 1`.                                                                           | 3      | No         |
| 39      | JUG         | r, i    | Unsigned `>`. Jump if `C = 0` and `Z = 0`.                                                                           | 3      | No         |
| 3a      | JUGE        | r, i    | Unsigned `>=`. Jump if `C = 0`.                                                                                      | 3      | No         |
| 3b      | JIL         | r, i    | Signed `<`. Jump if `S != O`.                                                                                        | 3      | No         |
| 3c      | JILE        | r, i    | Signed `<=`. Jump if `Z = 1` or `S != O`.                                                                            | 3      | No         |
| 3d      | JIG         | r, i    | Signed `>`. Jump if `Z = 0` and `S = O`.                                                                             | 3      | No         |
| 3e      | JIGE        | r, i    | Signed `>=`. Jump if `S = O`.                                                                                        | 3      | No         |
| 3f      | CALL        | r, i    | Push `ip` and jump (same target rules as `jmp`).                                                                     | 6      | No         |
| 40      | RET         |         | Pop a 32 bit value into `ip`.                                                                                        | 4      | No         |
| 41      | CPY         | r, r    | Block copy. Source address in `arg1`, length in `arg2`, destination address always taken from `r0`.                  | 256    | No         |
| 42      | CPY         | r, i    | Block copy. Source address in `arg1`, length is the immediate, destination from `r0`.                                | 256    | No         |
| 43      | CPY         | i, r    | Block copy. Source address is the immediate, length in `arg2`, destination from `r0`.                                | 256    | No         |
| 44      | CPY         | i, i    | Block copy. Source address is the first immediate, length is the second, destination from `r0`.                     | 256    | No         |
| 45      | DI          |         | Disable interrupts.                                                                                                  | 2      | Yes        |
| 46      | EI          |         | Enable interrupts.                                                                                                   | 2      | Yes        |
| 47      | IN          | r, r    | Read from a serial port. `arg1` = output register, `arg2` = port.                                                    | 12     | Yes        |
| 48      | IN          | r, i    | Read from a serial port. `arg1` = output register, immediate = port.                                                 | 12     | Yes        |
| 49      | OUT         | r, r    | Write to a serial port. `arg1` = port, `arg2` = data.                                                                | 12     | Yes        |
| 4a      | OUT         | r, i    | Write to a serial port. `arg1` = port, immediate = data.                                                             | 12     | Yes        |
| 4b      | OUT         | i, r    | Write to a serial port. Immediate = port, `arg2` = data.                                                             | 12     | Yes        |
| 4c      | OUT         | i, i    | Write to a serial port. First immediate = port, second = data.                                                       | 12     | Yes        |
| 4d      | NOP         |         | Do nothing for one cycle.                                                                                            | 1      | No         |
| 4e      | SHL         | r, r    | `arg1 = arg1 << arg2`.                                                                                               | 3      | No         |
| 4f      | SHL         | r, i    | `arg1 = arg1 << imm`.                                                                                                | 3      | No         |
| 50      | SHR         | r, r    | `arg1 = arg1 >> arg2` (logical shift).                                                                               | 3      | No         |
| 51      | SHR         | r, i    | `arg1 = arg1 >> imm` (logical shift).                                                                                | 3      | No         |
| 52      | IRET        |         | Return from an interrupt: pops the interrupt frame, restoring `mode`, `mbase`, `mlen`, `sp`, `fl` and `ip`.          | 8      | Yes        |
| 53      | SETIT       | r       | Set the interrupt-table pointer (`it`) from a register.                                                              | 2      | Yes        |
| 54      | SETIT       | i       | Set the interrupt-table pointer (`it`) from an immediate.                                                            | 2      | Yes        |
| 55      | GETIT       | r       | Read the interrupt-table pointer (`it`) into a register.                                                             | 2      | Yes        |
| 56      | SETKSP      | r       | Set the kernel stack pointer (`ksp`) from a register.                                                                | 2      | Yes        |
| 57      | SETKSP      | i       | Set the kernel stack pointer (`ksp`) from an immediate.                                                              | 2      | Yes        |
| 58      | GETKSP      | r       | Read the kernel stack pointer (`ksp`) into a register.                                                               | 2      | Yes        |
| 59      | SYSCALL     |         | Trigger interrupt `0x10` from any privilege tier (the only way for User mode to enter the kernel directly).          | 64     | No         |
| 5a      | UPTMS       |         | Read VM uptime in milliseconds. Low 32 bits go in `r0`, high 32 bits in `r1`.                                        | 12     | No         |
| 5b      | UPTNS       |         | Read VM uptime in nanoseconds. Low 32 bits go in `r0`, high 32 bits in `r1`.                                         | 8      | No         |
