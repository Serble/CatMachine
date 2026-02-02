# TODO

- Arrays
- Actual codegen

- Optmisation: make it so when binop arg is literal, don't bother using another register

# CatC Language Specification

This file contains all language specifications for the CatC programming language.

## Top Level
At the top level (outside a function), you may write regular code as well as function definitions.
The top level is the only place where function definitions and global variable declarations are allowed.
The only statements not allowed at the top level are `return` statements and 
local variable declarations. If you wish to define variables at the top level, use global variable declarations.

## Variables

```cc
let a:4;          // Declare var 'a' with 4 bytes
let b:4 = 5;      // Declare var 'b' with 4 bytes and initialize to 5
```

## Simple Operations
Most normal operations are supported.

`~*` - Signed multiply  
`~/` - Signed divide

## Inline ASM
```cc
// inputs                             outputs       clobbers
~~~r1[myVar:2 + 1], r2[myVar:2 + 2] | r0[myVar:4] | r3
mov r3, r0
mov r1, r3
int 0x90
~~~
```

## Functions

```cc
// 'a' will be allocated 2 bytes
// 'b' will be allocated 2 bytes
// The function returns the sum of 'a' and 'b' 
// using 2-byte integers
fun myFunc(a:2, b:2) {
    return a:2 + b:2;
}
```

## Structs

```cc
struct Thing {
    x: 4;
    y: 2;
    z: 2;
}

// define variable 'thing' and allocate space for a Thing struct
// $Thing is size of Thing struct
let thing:$Thing;

// set the 'x' field of 'thing' to 7 (using 4 bytes)
thing.Thing#x:4 = 7;

thing[Thing#x]:4 = 7;
(thing + Thing#x):4 = 7;


let myArr:8;
myArr[1+1,4]:4 = 5;

myArr:8 = 

// set whatIsX to the value of the 'x' field of 'thing' (using 4 bytes)
let whatIsX:4 = thing.Thing#x:4;
```


## Random Shit TODO
```cc
let a:4;

let a:4 = 5;

struct Thing {
    x: 4;
    y: 2;
    z: 2;
}

let thing:$Thing;
thing.Thing#x:4 = 7;

global myGlobal:2;

let whatIsX:4 = thing.Thing#x:4;

let myStr:4 = "hello world!";

let myStrPtr:4 = &myStr;


myFunc(whatIsX:4, "hello");

fun myFunc(a:2, b:2) {
    return a:2 + b:2;
}

signed:
~*
~/

unsigned:
*
/



// inputs                             outputs       clobbers
~~~r1[myVar:2 + 1], r2[myVar:2 + 2] | r0[myVar:4] | r3
mov r3, r0
mov r1, r3
int 0x90
~~~

OLD OUTDATED ARRAY STUFF:
let myArr:8;  // 8 bytes

myArr[1:4] = 5;

let myArrPtr* = &myArr;
*myArrPtr[1:4]

```
