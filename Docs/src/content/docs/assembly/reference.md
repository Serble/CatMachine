---
title: Cat Assembly Reference
slug: assembly/catasm-reference
---

The Cat assembler (`CatAssembler`) takes one or more `.cat` source files and emits a flat ROM binary alongside an optional debug-symbol file. Source is line-oriented and case-insensitive for mnemonics and register names.

Invocation:

```sh
CatAssembler <input.cat> [-o output.bin]
```

The assembler always writes a sibling `<output>.debug` file containing JSON debug symbols (see [Debug Symbols](#debug-symbols) below). The reference VM picks these up automatically when launched with `--debug`.

## Labels
Labels just define absolute memory addresses that can then be used as constants throughout the code.
A label is defined by writing the label name followed by a colon (`:`) at the beginning of a line. For example:
```cat
start:
    ; code here
```
Labels can be used in place of immediate values in instructions. For example:
```cat
    JMP start  ; Jump to the address defined by the label 'start'
```
Labels must be unique within a program. Duplicate labels will error.

Cat assembly supports three label kinds:

- **Scoped global labels** (default): `main:`  
  These are globally visible and define the local-label scope.
- **Local labels**: `.loop:`  
  These are only visible inside the nearest scoped global label.
- **Unscoped global labels**: `$print_char:`  
  These are globally visible but **do not** change the current local-label scope.

Local labels start with a dot (`.`). For example:
```cat
main:
    .loop:
        ; code here
        JMP .loop  ; Jump to the local label '.loop'
```
Local labels cannot be duplicated within the same global label scope, but can be reused in different global label scopes.

Unscoped global labels are useful for shared helpers/constants when you do not want to "break out" of the current scoped-global context:

```cat
main:
    .loop:
        CALL $print_char
        JMP .done

$print_char:
    ; helper routine
    RET

    .done:
        JMP .loop
```

You can reference unscoped global labels the same way in expressions (`JMP $entry`, `D32 $table_start`, etc.).
Labels can be defined before or after they are used in the code. The assembler will resolve the addresses during assembly.

## Comments
Commands are supported, all text after a semicolon (`;`) on a line is considered a comment and ignored by the assembler. For example:
```cat
    MOV R1, R2  ; This is a comment
    ; This entire line is a comment
```

## Data Directives
Data directives are used to directly insert data instead of encoding an instruction. The following data directives
will directly define data in memory:
- `D8` (Define Byte): Defines one or more bytes (8 bits each).
- `D16` (Define Short): Defines one or more shorts (16 bits each).
- `D32` (Define Word): Defines one or more words (32 bits each).
- `DSTR` (Define String): Defines bytes from a string literal, not null-terminated by default.
- `DFILE` (Define File): Inlines the raw contents of an external file into the ROM at the current position. The path is resolved relative to the directory containing the source file.
- `RES8` (Reserve Byte): Places a certain number of 0x00 bytes in the file.
- `RES16` (Reserve Short): Places a certain number of 0x0000 shorts in the file.
- `RES32` (Reserve Word): Places a certain number of 0x00000000 words in the file.

For examples:
```cat
mydata:
    D8  0x12, 0x34, 0x56       ; Defines three bytes
    D16 0x1234, 0x5678         ; Defines two shorts
    D32 0x12345678, 0x9ABCDEF0 ; Defines two words
    DSTR "Hello, World!\n\0"   ; Defines bytes for the string (including null terminator and newline)
    DFILE "sprite.bin"         ; Inlines the raw bytes of sprite.bin at this position

    RES8 4                     ; Same as D8 0, 0, 0, 0
    RES16 4                    ; Same as D16 0, 0, 0, 0
    RES32 3                    ; Same as D32 0, 0, 0
```

## Calling Convention

| Register | Use |
|----------|-----|
|  r0      | return value |  
|  r1      | first argument |  
|  r2      | second argument |  
|  r3      | third argument |  
| stack    | the rest of the arguments |

r0-3 inc is clobbered (Caller preserved)  
rest is not clobbered (Callee preserved)

For stack args they should be pushed right to left so that they can be popped in the natural order.

For example:  
```
1st Arg: a
2nd Arg: b
3rd Arg: c
4th Arg: d
5th Arg: e
6th Arg: f
```

then:
```cat
mov r1, a
mov r2, b
mov r3, c
push f
push e
push d
call someFunc
```

## Memory Access
To specify that you want to access memory, wrap the address source in `[]`. For example:
```cat
    MOV R1, [R2]      ; Load the value from the memory address in R2 into R1
    MOV [0x1000], R3  ; Store the value in R3 into memory address 0x1000
```

## Defines and Values
Any constant, label, or number literal may be used where numbers go. For example:
```cat
JMP 0x00
JMP main
JMP variablename
```
are all valid, as long as those labels/constants exist.

You may define constants using  
```cat
#const VAR_NAME, value
```
The value of the constant can also be any numerical input, including a label.

As well as using a mix of these types, you may also combine them in mathematical expressions,
that will be evaluated at compile time. The order in which you define constants is
also meaningless, you may use them before they are defined. For example:

```cat
#const A, 5
#const B, A*7 + 1
#const C, B-main

main:
    JMP A+C/B
```
Although this would likely cause a runtime error this is completely valid syntax.
And all these values will be evaluated just fine. But be careful, circular dependencies
will cause errors.

## Macros
You may define small code snippets called macros which can be used like an
instruction would and get expanded at assemble time.
```cat
#macro debug, 1
push r1
mov r1, $1
int 0x90
pop r1
#endmacro
```
> Here is an example of a debug macro which preserves r1 and debug prints
> the number passed through. To use it you would simply write:
> `debug SOMENUMBER` or `debug r1`.

The `, 1` specifies how many arguments the macro has. Each
argument is referenced as `$NUM` where NUM is the number of 
the argument starting from 1 (first arg is `$1`). These arguments
are textually replaced at assemble time, so you can have whatever
you like in them.

Example macro that prints 3 numbers:
```cat
#define A, 7+8

#macro print_three, 3
mov r1, $1
int 0x90
mov r1, $2
int 0x90
mov r1, $3
int 0x90
#endmacro

main:
    mov r4, 0xFF
    print_three A, main - 3, r4
```
## Includes

You may pull in another source file as if it had been pasted at the point of the directive:

```cat
#include "std.cat"
#include "graphics/sprites.cat"
```

The path is resolved relative to the directory containing the source file performing the include. Included files participate in the same global label and constant namespace, so they can refer to (and be referred to by) anything in the including file.

There is no include guard – including the same file twice will produce duplicate-label errors. The convention is to include each support file exactly once from a top-level entry file.

## Jump-Style Mnemonics

The conditional jump family (`jmp`, `jz`/`je`, `jnz`/`jne`, `jul`, `jule`, `jug`, `juge`, `jil`, `jile`, `jig`, `jige`) and `call` accept a single address-shaped operand in assembly. The assembler automatically encodes the underlying two-argument form `(register, immediate)`:

- `jmp label`     – encoded as `(0xFF, label)`. The CPU treats register `0xFF` as "no base", so this is an absolute jump.
- `jmp r1`        – encoded as `(r1, 0)`. Jumps to whatever absolute address `r1` holds.
- `jmp r1 + label`– encoded as `(r1, label)`. Useful for jump tables.

The same shorthand applies to `call`.

## Debug Symbols

Whenever the assembler produces an output file it also writes a sibling `<output>.debug` JSON file containing a [`DebugTable`](https://github.com/Serble/CatMachine/blob/master/CatData/DebugTable.cs):

```json
{
  "Symbols": [
    { "FilePos": 0,  "Line": 12, "RawLine": "mov r1, 5" },
    { "FilePos": 6,  "Line": 13, "RawLine": "add r1, r2" }
  ],
  "Labels": {
    "main":   0,
    "loop":   16
  }
}
```

- `Symbols` records, for every assembled instruction, the byte offset in the output (`FilePos`), the original source line number, and the un-tokenised text of the line that produced it.
- `Labels` is a flat map from label name to its assembled address. It contains both global and local labels (with their fully-qualified names).

The file is JSON for ease of consumption by external tooling. The reference VM loads it automatically when launched as `CatVM <rom> --debug`, enabling source-line lookup, symbolic breakpoints (`break symbol main`, `break line 42`), and stack traces that show function names instead of bare addresses.

The same record types live in the `CatData` project so that any other tool (Catnip compiler, future debuggers, IDE plugins) can produce or consume `.debug` files without depending on the assembler.
