/*
 * catvm-libc - a tiny C standard library for the Cat VM
 * =====================================================
 *
 * Provides just enough to write small programs that print things, read input,
 * do basic memory operations, and exit. Everything ultimately funnels through
 * the four CatVM intrinsics provided by the CatLLVM backend:
 *
 *   __catvm_int(num)         - raise interrupt 'num'
 *   __catvm_in(port)         - read a 32-bit word from a port
 *   __catvm_out(port, val)   - write a 32-bit word to a port
 *   __catvm_syscall()        - invoke the syscall opcode
 *
 * If you have clang installed, you can rebuild libc.ll like this:
 *
 *   clang -S -emit-llvm -O0 -m32 -ffreestanding -nostdlib \
 *         -target i386-unknown-none catvm.c -o catvm.ll
 *
 * If you don't, the hand-translated catvm.ll alongside this file works just
 * the same.
 */

#ifndef CATVM_LIBC_H
#define CATVM_LIBC_H

typedef unsigned char       uint8_t;
typedef signed char         int8_t;
typedef unsigned short      uint16_t;
typedef signed short        int16_t;
typedef unsigned int        uint32_t;
typedef signed int          int32_t;
typedef unsigned int        size_t;

/* CatVM intrinsics - implemented directly by the CatLLVM backend */
extern void     __catvm_int(uint8_t num);
extern uint32_t __catvm_in(uint32_t port);
extern void     __catvm_out(uint32_t port, uint32_t val);
extern void     __catvm_syscall(void);
extern void     __catvm_print(const char *s);   /* int 0x80 with r1 = s */
extern uint32_t __catvm_uptime(void);           /* int 0x85, returns r0 */

/* I/O ----------------------------------------------------------------------- */

/* Print a NUL-terminated C string to stdout. Implemented via int 0x80 which
 * reads the address from r1 and prints chars until it sees a 0x00 byte. */
void puts_raw(const char *s);

/* Print a string then a newline. */
void puts(const char *s);

/* Print a single character. */
void putchar(char c);

/* Print a signed 32-bit integer in decimal. */
void puti(int32_t n);

/* Print an unsigned 32-bit integer in decimal. */
void putu(uint32_t n);

/* Print an unsigned 32-bit integer in hex (no leading "0x", no padding). */
void putx(uint32_t n);

/* Process control --------------------------------------------------------- */

/* Shut down the VM - never returns. */
void exit(int code);

/* Halt (pause) the VM - returns once something un-pauses it. */
void halt(void);

/* Get milliseconds since boot. */
uint32_t uptime_ms(void);

/* Memory ------------------------------------------------------------------ */

void   *memset(void *dst, int byte, size_t n);
void   *memcpy(void *dst, const void *src, size_t n);
size_t  strlen(const char *s);
int     strcmp(const char *a, const char *b);

#endif /* CATVM_LIBC_H */
