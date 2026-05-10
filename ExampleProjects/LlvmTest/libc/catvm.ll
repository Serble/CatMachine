; ============================================================================
; CatVM-libc - hand-translated LLVM IR for ExampleProjects/LlvmTest/libc/catvm.c
;
; This is the file the CatLLVM backend actually consumes. It mirrors catvm.c
; one-to-one. Once a real `clang --target=catvm` exists we can replace this
; with the compiler's output instead.
; ============================================================================

; ---- intrinsics implemented directly by the CatLLVM backend ---------------
declare void @__catvm_int(i8)
declare i32  @__catvm_in(i32)
declare void @__catvm_out(i32, i32)
declare void @__catvm_syscall()
declare void @__catvm_print(ptr)
declare i32  @__catvm_uptime()

; ============================================================================
; Output primitives
; ============================================================================

; void puts_raw(const char *s) - print a NUL-terminated string, no newline.
define void @puts_raw(ptr %s) {
entry:
  call void @__catvm_print(ptr %s)
  ret void
}

; void putchar(char c) - print a single character via a 2-byte stack buffer.
define void @putchar(i8 %c) {
entry:
  %buf = alloca [2 x i8]
  %p0 = getelementptr inbounds [2 x i8], ptr %buf, i32 0, i32 0
  store i8 %c, ptr %p0
  %p1 = getelementptr inbounds [2 x i8], ptr %buf, i32 0, i32 1
  store i8 0, ptr %p1
  call void @puts_raw(ptr %p0)
  ret void
}

; void puts(const char *s) - print a string followed by a newline.
define void @puts(ptr %s) {
entry:
  call void @puts_raw(ptr %s)
  call void @putchar(i8 10)
  ret void
}

; void putu(uint32_t n) - print an unsigned 32-bit integer in base 10.
define void @putu(i32 %n) {
entry:
  %buf = alloca [12 x i8]
  %idxp = alloca i32
  %nvp  = alloca i32
  store i32 11, ptr %idxp
  store i32 %n, ptr %nvp

  ; buf[11] = '\0'
  %i0 = load i32, ptr %idxp
  %p0 = getelementptr inbounds [12 x i8], ptr %buf, i32 0, i32 %i0
  store i8 0, ptr %p0
  %i1 = sub i32 %i0, 1
  store i32 %i1, ptr %idxp

  %nv0 = load i32, ptr %nvp
  %iszero = icmp eq i32 %nv0, 0
  br i1 %iszero, label %zero, label %loop

zero:
  %iz = load i32, ptr %idxp
  %pz = getelementptr inbounds [12 x i8], ptr %buf, i32 0, i32 %iz
  store i8 48, ptr %pz                ; '0'
  %iz2 = sub i32 %iz, 1
  store i32 %iz2, ptr %idxp
  br label %print

loop:
  %nv1 = load i32, ptr %nvp
  %nz = icmp eq i32 %nv1, 0
  br i1 %nz, label %print, label %body

body:
  %nv2 = load i32, ptr %nvp
  %d   = urem i32 %nv2, 10
  %d8  = trunc i32 %d to i8
  %dig = add i8 %d8, 48
  %ii  = load i32, ptr %idxp
  %pp  = getelementptr inbounds [12 x i8], ptr %buf, i32 0, i32 %ii
  store i8 %dig, ptr %pp
  %ii2 = sub i32 %ii, 1
  store i32 %ii2, ptr %idxp
  %nq  = udiv i32 %nv2, 10
  store i32 %nq, ptr %nvp
  br label %loop

print:
  %ip  = load i32, ptr %idxp
  %ip1 = add i32 %ip, 1
  %ps  = getelementptr inbounds [12 x i8], ptr %buf, i32 0, i32 %ip1
  call void @puts_raw(ptr %ps)
  ret void
}

; void puti(int32_t n) - print a signed 32-bit integer in base 10.
define void @puti(i32 %n) {
entry:
  %neg = icmp slt i32 %n, 0
  br i1 %neg, label %is_neg, label %is_pos

is_neg:
  call void @putchar(i8 45)            ; '-'
  %nn = sub i32 0, %n
  call void @putu(i32 %nn)
  ret void

is_pos:
  call void @putu(i32 %n)
  ret void
}

; void putx(uint32_t n) - print an unsigned 32-bit integer in lowercase hex.
define void @putx(i32 %n) {
entry:
  %buf = alloca [12 x i8]
  %idxp = alloca i32
  %nvp  = alloca i32
  store i32 11, ptr %idxp
  store i32 %n, ptr %nvp

  %i0 = load i32, ptr %idxp
  %p0 = getelementptr inbounds [12 x i8], ptr %buf, i32 0, i32 %i0
  store i8 0, ptr %p0
  %i1 = sub i32 %i0, 1
  store i32 %i1, ptr %idxp

  %nv0 = load i32, ptr %nvp
  %iszero = icmp eq i32 %nv0, 0
  br i1 %iszero, label %zero, label %loop

zero:
  %iz = load i32, ptr %idxp
  %pz = getelementptr inbounds [12 x i8], ptr %buf, i32 0, i32 %iz
  store i8 48, ptr %pz
  %iz2 = sub i32 %iz, 1
  store i32 %iz2, ptr %idxp
  br label %print

loop:
  %nv1 = load i32, ptr %nvp
  %nz = icmp eq i32 %nv1, 0
  br i1 %nz, label %print, label %body

body:
  %nv2 = load i32, ptr %nvp
  %d   = and i32 %nv2, 15
  %hi  = icmp uge i32 %d, 10
  br i1 %hi, label %hex, label %dec

dec:
  %da = trunc i32 %d to i8
  %ca = add i8 %da, 48                 ; '0'
  br label %store_d

hex:
  %db = trunc i32 %d to i8
  %cb = add i8 %db, 87                 ; 'a' - 10
  br label %store_d

store_d:
  %ch = phi i8 [ %ca, %dec ], [ %cb, %hex ]
  %ii = load i32, ptr %idxp
  %pp = getelementptr inbounds [12 x i8], ptr %buf, i32 0, i32 %ii
  store i8 %ch, ptr %pp
  %ii2 = sub i32 %ii, 1
  store i32 %ii2, ptr %idxp
  %nq = lshr i32 %nv2, 4
  store i32 %nq, ptr %nvp
  br label %loop

print:
  %ip  = load i32, ptr %idxp
  %ip1 = add i32 %ip, 1
  %ps  = getelementptr inbounds [12 x i8], ptr %buf, i32 0, i32 %ip1
  call void @puts_raw(ptr %ps)
  ret void
}

; ============================================================================
; Process control
; ============================================================================

; void exit(int code) - shut down the VM (int 0x82). Code is ignored for now.
define void @exit(i32 %code) {
entry:
  call void @__catvm_int(i8 -126)      ; 0x82, encoded as signed i8
  ret void
}

; void halt(void) - pause the VM until interrupted (int 0x81).
define void @halt() {
entry:
  call void @__catvm_int(i8 -127)      ; 0x81
  ret void
}

; uint32_t uptime_ms(void)
define i32 @uptime_ms() {
entry:
  %t = call i32 @__catvm_uptime()
  ret i32 %t
}

; ============================================================================
; Memory primitives
; ============================================================================

; void *memset(void *dst, int byte, size_t n)
define ptr @memset(ptr %dst, i32 %byte, i32 %n) {
entry:
  %ip = alloca i32
  store i32 0, ptr %ip
  br label %loop

loop:
  %iv  = load i32, ptr %ip
  %cmp = icmp ult i32 %iv, %n
  br i1 %cmp, label %body, label %done

body:
  %p  = getelementptr inbounds i8, ptr %dst, i32 %iv
  %b8 = trunc i32 %byte to i8
  store i8 %b8, ptr %p
  %iv2 = add i32 %iv, 1
  store i32 %iv2, ptr %ip
  br label %loop

done:
  ret ptr %dst
}

; void *memcpy(void *dst, const void *src, size_t n)
define ptr @memcpy(ptr %dst, ptr %src, i32 %n) {
entry:
  %ip = alloca i32
  store i32 0, ptr %ip
  br label %loop

loop:
  %iv  = load i32, ptr %ip
  %cmp = icmp ult i32 %iv, %n
  br i1 %cmp, label %body, label %done

body:
  %sp = getelementptr inbounds i8, ptr %src, i32 %iv
  %v  = load i8, ptr %sp
  %dp = getelementptr inbounds i8, ptr %dst, i32 %iv
  store i8 %v, ptr %dp
  %iv2 = add i32 %iv, 1
  store i32 %iv2, ptr %ip
  br label %loop

done:
  ret ptr %dst
}

; size_t strlen(const char *s)
define i32 @strlen(ptr %s) {
entry:
  %np = alloca i32
  store i32 0, ptr %np
  br label %loop

loop:
  %nv = load i32, ptr %np
  %p  = getelementptr inbounds i8, ptr %s, i32 %nv
  %c  = load i8, ptr %p
  %z  = icmp eq i8 %c, 0
  br i1 %z, label %done, label %inc

inc:
  %nv2 = add i32 %nv, 1
  store i32 %nv2, ptr %np
  br label %loop

done:
  %r = load i32, ptr %np
  ret i32 %r
}

; int strcmp(const char *a, const char *b)
define i32 @strcmp(ptr %a, ptr %b) {
entry:
  %ap = alloca ptr
  %bp = alloca ptr
  store ptr %a, ptr %ap
  store ptr %b, ptr %bp
  br label %loop

loop:
  %av = load ptr, ptr %ap
  %bv = load ptr, ptr %bp
  %ac = load i8, ptr %av
  %bc = load i8, ptr %bv
  %az = icmp eq i8 %ac, 0
  br i1 %az, label %done, label %ckb

ckb:
  %eq = icmp eq i8 %ac, %bc
  br i1 %eq, label %adv, label %done

adv:
  %ap2 = getelementptr inbounds i8, ptr %av, i32 1
  %bp2 = getelementptr inbounds i8, ptr %bv, i32 1
  store ptr %ap2, ptr %ap
  store ptr %bp2, ptr %bp
  br label %loop

done:
  %az2 = zext i8 %ac to i32
  %bz2 = zext i8 %bc to i32
  %r   = sub i32 %az2, %bz2
  ret i32 %r
}
