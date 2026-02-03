
fun debug_num(n:4) {
    ~~~r1[n:4] | | ;
    int 0x90
    ~~~
}

fun print(s:4) {
    ~~~r1[s:4] | | ;
    int 0x80
    ~~~
}

fun get_display_buffer() {
    ~~~ | | ;
    int 0x84
    ~~~
    // r0 is now the display buffer address
}

fun update_display() {
    ~~~ | | ;
    int 0x86
    ~~~
}

fun halt() {
    ~~~ | | ;
    int 0x81
    ~~~
}

fun shutdown() {
    ~~~ | | ;
    int 0x82
    ~~~
}

fun reset() {
    ~~~ | | ;
    int 0x83
    ~~~
}

fun get_uptime() {
    ~~~ | | ;
    int 0x85
    ~~~
    // r0 is now the uptime
    // so we can just return
}

// Returns -1 if no input is available
// otherwise returns the keycode that was pressed
// type should be ptr
fun poll_input(type:4) {
    let val:4;
    ~~~ | r1[val:4] | ;
    in r1, 0
    ~~~
    
    if (val:4 == -1) {
        return -1;
    }
    
    // Consume type
    ~~~ | r1[val:4] | ;
    in r1, 0
    ~~~
    (type:4):4 = val:4;
    
    // Consume value
    ~~~ | r1[val:4] | ;
    in r1, 0
    ~~~
    
    return val:4;
}

