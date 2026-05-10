/* See catvm.h for the public API. This is the reference C source for the
 * standard library; the hand-translated catvm.ll is what actually links into
 * the test program when clang isn't available.
 */
#include "catvm.h"

/* ------------------------------------------------------------------------- */
/* I/O                                                                       */
/* ------------------------------------------------------------------------- */

void puts_raw(const char *s) {
    /* int 0x80 needs the string pointer in r1; __catvm_print does that for us. */
    __catvm_print(s);
}

void putchar(char c) {
    /* Build a tiny stack-allocated 2-byte string [c, 0] and print it. */
    char buf[2];
    buf[0] = c;
    buf[1] = 0;
    puts_raw(buf);
}

void puts(const char *s) {
    puts_raw(s);
    putchar('\n');
}

/* Decimal print for unsigned 32-bit. Builds digits into a stack buffer right
 * to left (max 10 digits + NUL) then calls puts_raw. */
void putu(uint32_t n) {
    char buf[12];
    int idx = 11;
    buf[idx--] = 0;
    if (n == 0) {
        buf[idx--] = '0';
    } else {
        while (n != 0) {
            uint32_t d = n % 10;
            buf[idx--] = (char)('0' + d);
            n = n / 10;
        }
    }
    puts_raw(&buf[idx + 1]);
}

void puti(int32_t n) {
    if (n < 0) {
        putchar('-');
        /* Trick: -INT_MIN doesn't fit in i32, but unsigned cast handles it. */
        putu((uint32_t)(-n));
    } else {
        putu((uint32_t)n);
    }
}

void putx(uint32_t n) {
    char buf[12];
    int idx = 11;
    buf[idx--] = 0;
    if (n == 0) {
        buf[idx--] = '0';
    } else {
        while (n != 0) {
            uint32_t d = n & 0xF;
            buf[idx--] = (char)(d < 10 ? '0' + d : 'a' + d - 10);
            n = n >> 4;
        }
    }
    puts_raw(&buf[idx + 1]);
}

/* ------------------------------------------------------------------------- */
/* Process control                                                           */
/* ------------------------------------------------------------------------- */

void exit(int code) {
    (void)code;          /* CatVM has no exit codes */
    __catvm_int(0x82);   /* shutdown */
}

void halt(void) {
    __catvm_int(0x81);
}

uint32_t uptime_ms(void) {
    return __catvm_uptime();
}

/* ------------------------------------------------------------------------- */
/* Memory                                                                    */
/* ------------------------------------------------------------------------- */

void *memset(void *dst, int byte, size_t n) {
    char *d = (char*)dst;
    for (size_t i = 0; i < n; i++) d[i] = (char)byte;
    return dst;
}

void *memcpy(void *dst, const void *src, size_t n) {
    char *d = (char*)dst;
    const char *s = (const char*)src;
    for (size_t i = 0; i < n; i++) d[i] = s[i];
    return dst;
}

size_t strlen(const char *s) {
    size_t n = 0;
    while (s[n] != 0) n++;
    return n;
}

int strcmp(const char *a, const char *b) {
    while (*a != 0 && *a == *b) { a++; b++; }
    return (int)(uint8_t)*a - (int)(uint8_t)*b;
}
