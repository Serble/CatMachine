#include "std.cc"
#define WORD, 4

global asd:${WORD} = 5; 
debug_num(asd:${WORD} * 0);

asd:${WORD} = asd:${WORD} + 0;

print("hello world!");

0:4 = 5;

main();

fun main() {
    let a:4 = hello;
    (a:4)();
    
    // let's use a struct
    let t:$Thingy;
    t[Thingy#a]:4 = 42;
        
    debug_num(Thingy#a);
    
    debug_num(t[Thingy#a]:4);
        
    let myArr:16;
    myArr[0,4]:4 = 1;
    myArr[1,4]:4 = 2;
    myArr[2,4]:4 = 3;
    myArr[3,4]:4 = 4;
    
    debug_num(myArr[0,4]:4);
    debug_num(myArr[1,4]:4);
    debug_num(myArr[2,4]:4);
    debug_num(myArr[3,4]:4);
    
    while (!0) {
        
    }
}

fun hello(a:4) {
    
}

struct Thingy {
    a:4;
    b:2;
    c:2;
    d:1;
}
