
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
