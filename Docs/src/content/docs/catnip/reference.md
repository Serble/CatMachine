---
title: Catnip Reference
slug: catnip/catnip-reference
---

**Catnip** is a low-level, explicit, systems language. It exposes memory and sizes directly with little abstraction. 
No real notion of "types"; only *sizes*, pointers, offsets, and raw values matter.

---

## Table of Contents
1. [Directives](#directives)
2. [Top Level](#top-level)
3. [Variables and Memory](#variables-and-memory)
4. [Expressions and Dereferencing](#expressions-and-dereferencing)
5. [Operators](#operators)
6. [Functions](#functions)
7. [Structs](#structs)
8. [Arrays and Indexing](#arrays-and-indexing)
9. [Literals](#literals)
10. [Global Variables and Constants](#global-variables-and-constants)
11. [Inline Assembly](#inline-assembly)
12. [Function Calls & Calling Pointers](#function-calls--calling-pointers)
13. [Miscellaneous Syntax Quirks](#miscellaneous-syntax-quirks)

---

## Directives

### `#include`
Includes and inlines the full contents of another file.
```nip
#include "std.cc"
```

### `#define`
Declares a macro for **textual substitution**. Works as a preprocessor with copy-paste behaviour.
Use them with `${NAME}` syntax.
```nip
#define WORD, 4
#define SOME_VALUE 12345
let value:${WORD} = ${SOME_VALUE};
```
Arguments are not "typed" or checked; insertion is by direct substitution.

---

## Top Level

- Top-level code (not inside any function) allows:
    - Expressions/assignments
    - Global variable declarations
    - Function or struct definitions
- Disallowed at top-level:
    - `return` statements
    - Local variable declarations (`let` must be inside a function)
```nip
global foo:7 = 11;
fun bar() { ... }
struct Thing { ... }
```

---

## Variables and Memory

- **Variables** do not have types, only *size in bytes*.
- Any non-zero size ≥1 is supported on allocation, e.g., `let raw:13;` or `let data:921;`
- On dereferencing, `a:1`, `a:2`, or `a:4` are required (1, 2, or 4 bytes) due to hardware constraints.

### Local Variable
```nip
fun foo() {
    let buf:1337;       // 1337 bytes local space
    buf[12]:1 = 0x7F;   // Set 13th byte
}
```

### Global Variable
```nip
global arr:6;
global big:90001;
```

---

## Expressions and Dereferencing

- The `a:4` (variable colon number) **dereferences the value at pointer `a`, reading or writing 4 bytes.**
    - **Dereferencing a literal is unsafe.** `10:4` is technically allowed but will try to read memory at address 10, 
  which is likely invalid, or code, so use carefully.
    - You can dereference any expression, so be careful with it, there is zero safety checking.

```nip
let buf:8;
buf:4 = 123;          // Dereference pointer 'buf', 4 bytes
buf[4]:2 = 0x9;       // Deref 'buf + 4', 2 bytes
buf[1,4]:2 = 0x9;     // Deref 'buf + 1*4', 2 bytes (same as above)

let ptr:4 = buf;      // BOTH 'buf' and 'ptr' are pointers (addresses)
ptr:4 = 999;          // Write at 'ptr' (will overwrite the buf address we previously set)
```

---

### Legal and Illegal Dereference

| Syntax         | Meaning                                                    | Valid? |
|----------------|------------------------------------------------------------|--------|
| buf:4          | Dereference 4 bytes at address stored in 'buf'             | Yes    |
| (myArr+1):1    | Dereference 1 byte at address 'myArr + 1'                  | Yes    |
| 10:2           | Dereference 2 bytes at address 10 (dangerous but possible) | Yes?   |
| 7:8            | Dereference 8 bytes at address 7 (size not supported)      | No     |


> **NOTE:** You may only dereference using supported sizes for load/store (`1`, `2`, or `4`) as these correspond to real hardware operations.

---

## Operators

- **Arithmetic:** `+`, `-`, `*`, `/`, `%`
- **Signed Operations:** Prefix with `~`
    - `~*` (signed multiply), `~/` (signed divide), `~%` (signed modulus)
- **Comparison:** `==`, `!=`, `<`, `>`, `<=`, `>=` (All less-than/greater-than support signed variants with `~` prefix)
- **Bitwise/Logical:** `&`, `|`, `^`, `~`, `!`, etc.
- **Assignment:** `=`

```nip
let v:4 = a:4 ~* 123;    // Signed multiply
let v:4 = a:4 * 123;     // Unsigned multiply

let isNeg:4 = (a:4 ~< 0) ; // Signed less-than comparison
let isLess:4 = (a:4 < 100); // Unsigned less-than comparison
```

---

## Functions

- Define with `fun`:
```nip
fun add(a:4, b:4) {
    return a:4 + b:4;
}
```
- Parameter sizes are **how much argument data is loaded in (by convention, in registers)**
- The **function symbol itself is a pointer** (address).

#### Returning:
- You can omit `return`; whatever is in `r0` will be function result (possibly garbage).
- Specifying `return;` without an expression yields the same effect.
- If you want a value, use `return expr;`.

```nip
fun myFunc() {
    // result is not specified, whatever is in r0 register
}

func myFunc2() {
    return 42;  // returns 42 to caller
}

func myFunc3(a:4) {
    if (a:4) {
        return 1;
    }
    // don't return any value
}

// res gets whatever was in r0, which is unsafe but allowed
let res:4 = myFunc();

// res2 gets 42
let res2:4 = myFunc2();

// res3 gets 1 if someVal is non-zero, else garbage
let res3:4 = myFunc3(someVal:4);
```
- No typechecking or size enforcement is done. It's your job to keep your conventions straight.

---

## Structs

- Structs describe **contiguous blocks of bytes with named field offsets and sizes.
- No "types" attached to variables; struct just provides memory map and size/offset info.

```nip
struct Thing {
    x: 4;
    y: 2;
    z: 1;
}
let foo:$Thing;             // Allocates enough space (7 bytes)
foo[Thing#y]:2 = 42;        // Writes 42 to offset 4 (y field)
let v:2 = foo[Thing#y]:2;   // Reads 2 bytes at offset 4

// Equivalent to: (foo + Thing#y):2 = 42;
```

#### Special Struct Symbols:

- `$Thing` — size of the struct (sum of field widths).
- `Thing#field` — offset of `field` from start of the struct in bytes.

**No complex field access — no `foo.Thing#y` or dot syntax. Use indexes or pointer math.**

---

## Arrays and Indexing

- Variables are just memory. Create arrays by picking size.

```nip
let data:16;                    // 16 bytes

data[0]:1 = 9;                  // set byte 0 to 9
data[3,4]:4 = 77;               // set 12th-15th byte (index 3 of 4-byte elements) to 77
let w:4 = data[3,4]:4;          // read index 3 as 4-byte int

data[i,s]    →   (data + i*s)
data[i]      →   (data + i)
```


---

## Literals

- Only numbers and quoted strings.
- **Strings are pointers to their data** (address placed into code or memory).

```nip
let s:4 = "hello";       // s is a pointer to the string data
let p:4 = s;             // p is a pointer to s (a pointer to a pointer)
```

---

## Global Variables and Constants

- Use `global` at top level for globals, any size:
```nip
global bigbuf:1024;
global flag:1 = 0;
```
- Use `#define` for text macros (see above).

---

## Inline Assembly

- Enclosed between `~~~` markers.
- First line: register mappings (inputs | outputs | clobbers).  
- Use `register[expr]` to map inputs to registers.
- Use `register[pointer:size]` expressions for mapping variables for out parameters.

```nip
~~~ r1[n:4], r2[tmp:4] | r0[result:4] | r1, r2
mov r1, r0
add r2, r1
int 0x90
~~~
```

- The sections before/after `|` specify input regs, output regs, and clobbered regs, respectively.
- Outputs must be dereferences to write back to memory.
- Unlike outputs, inputs can be any expression whatsoever.

| Output Example     | Meaning                                          | Valid? |
|--------------------|--------------------------------------------------|--------|
| `r1[result:4]`     | Place the value from r1 into 4 bytes of `result` | Yes    |
| `r0`               | Missing target expression                        | No     |
| `r2[tmp]`          | Missing size on dereference                      | No     |
| `r1[(main + 4):4]` | Valid dereference expression                     | Yes    |

---

## Function Calls & Calling Pointers

- Any pointer can be called if it points to code.
```nip
// reserve 4 bytes and place the pointer to main in it
let f:4 = main;

// dereference and call the function pointer
f:4();

// Call with offset
(main + 4)();

// Call the 'add' function except go back 1 byte 
// (likely to error at runtime, but allowed syntax)
(add - 1)();
```
- Arguments supplied by value.

---

## Miscellaneous Syntax Quirks

- **Variables, functions, and struct names are always interpreted as addresses when used in expressions**.
- No "type", only explicit size at allocation/dereference.
- You may pass literal pointers, and can dereference numbers or string literals.
- **Dereferencing (`x:size`)** is pointer read/write.

---
