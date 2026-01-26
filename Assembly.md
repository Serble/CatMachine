# Cat Assembly Language Specification

## Labels
Labels just define absolute memory addresses that can then be used as constants throughout the code.
A label is defined by writing the label name followed by a colon (`:`) at the beginning of a line. For example:
```asm
start:
    ; code here
```
Labels can be used in place of immediate values in instructions. For example:
```asm
    JMP start  ; Jump to the address defined by the label 'start'
```
Labels must be unique within a program. Duplicate labels will error.
Labels can however be local and are scoped to the nearest global label. Local labels start with a dot (`.`). For example:
```asm
main:
    .loop:
        ; code here
        JMP .loop  ; Jump to the local label '.loop'
```
Local labels cannot be duplicated within the same global label scope, but can be reused in different global label scopes.
Labels can be defined before or after they are used in the code. The assembler will resolve the addresses during assembly.

## Comments
Commands are supported, all text after a semicolon (`;`) on a line is considered a comment and ignored by the assembler. For example:
```asm
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

For examples:
```asm
mydata:
    D8  0x12, 0x34, 0x56       ; Defines three bytes
    D16 0x1234, 0x5678         ; Defines two shorts
    D32 0x12345678, 0x9ABCDEF0 ; Defines two words
    DSTR "Hello, World!\n\0"   ; Defines bytes for the string (including null terminator and newline)
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

## Memory Access
To specify that you want to access memory, wrap the address source in `[]`. For example:
```asm
    MOV R1, [R2]      ; Load the value from the memory address in R2 into R1
    MOV [0x1000], R3  ; Store the value in R3 into memory address 0x1000
```

