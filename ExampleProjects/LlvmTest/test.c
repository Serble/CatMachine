/* test.c - exercises the CatVM-libc.
 *
 * Build & run with:
 *     bash run.sh
 */
#include "libc/catvm.h"

int main(void) {
    puts("Hello from CatVM!");

    /* Fibonacci (signed, base 10) */
    puts_raw("fib(0..10) = ");
    int a = 0, b = 1;
    for (int i = 0; i <= 10; i++) {
        puti(a);
        putchar(' ');
        int c = a + b;
        b = a;
        a = c;
    }
    putchar('\n');

    /* uptime */
    puts_raw("uptime ms = ");
    putu(uptime_ms());
    putchar('\n');

    /* hex */
    puts_raw("0xdeadbeef = ");
    putx(0xdeadbeef);
    putchar('\n');

    /* strcmp */
    if (strcmp("cat", "cat") == 0)         puts("strcmp(cat,cat) == 0   ok");
    if (strcmp("apple", "banana") < 0)     puts("strcmp(apple,banana) < 0 ok");
    if (strcmp("zebra", "apple") > 0)      puts("strcmp(zebra,apple) > 0 ok");

    /* memset / memcpy */
    char buf[16];
    memset(buf, 'X', 5);
    buf[5] = 0;
    puts_raw("memset 5 X: '"); puts_raw(buf); puts("'");

    memcpy(buf, "hello", 6);
    puts_raw("memcpy hello: '"); puts_raw(buf); puts("'");

    /* strlen */
    puts_raw("strlen('hello') = ");
    putu(strlen("hello"));
    putchar('\n');

    puts("bye!");
    exit(0);
    return 0;
}
