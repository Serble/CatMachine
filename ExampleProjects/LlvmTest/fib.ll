; Sample LLVM IR exercising several features of the CatLLVM backend.
; Compute fib(10) via iteration and emit it via __catvm_int(0x90) which is
; FuncWriteStdout in CatVM.
;
; This file is hand-written so we can exercise specific code paths without
; needing clang installed.

@result = global i32 0

declare void @__catvm_int(i8)

define i32 @fib(i32 %n) {
entry:
  %a = alloca i32
  %b = alloca i32
  %i = alloca i32
  store i32 0, ptr %a
  store i32 1, ptr %b
  store i32 0, ptr %i
  br label %loop

loop:
  %iv = load i32, ptr %i
  %cmp = icmp slt i32 %iv, %n
  br i1 %cmp, label %body, label %done

body:
  %av = load i32, ptr %a
  %bv = load i32, ptr %b
  %sum = add i32 %av, %bv
  store i32 %bv, ptr %a
  store i32 %sum, ptr %b
  %iv2 = load i32, ptr %i
  %iv3 = add i32 %iv2, 1
  store i32 %iv3, ptr %i
  br label %loop

done:
  %ret = load i32, ptr %a
  ret i32 %ret
}

define i32 @main() {
entry:
  %r = call i32 @fib(i32 10)
  store i32 %r, ptr @result
  ; print %r via test interrupt 0x90 (CatVM prints whatever is in r1 as a number)
  call void @print_num(i32 %r)
  ; emit a shutdown interrupt (0x82) when done
  call void @__catvm_int(i8 130)
  ret i32 0
}

; Print whatever value is in r1 (CatVM testing interrupt 0x90 = 144)
; CatVM's calling convention uses r1 for arg 0, so the parameter lands
; in r1 automatically before we trigger the interrupt.
define void @print_num(i32 %v) {
  call void @__catvm_int(i8 144)
  ret void
}
