; Exercise: globals + array GEP + signed compare + nested if/else.
; Computes sum of all positive entries in a 5-element array.

@nums = global [5 x i32] [i32 -3, i32 4, i32 -1, i32 7, i32 2]

declare void @__catvm_int(i8)

define void @print_num(i32 %v) {
  call void @__catvm_int(i8 144)   ; CatVM testing int 0x90: prints r1
  ret void
}

define i32 @sum_pos(ptr %arr, i32 %n) {
entry:
  %i = alloca i32
  %s = alloca i32
  store i32 0, ptr %i
  store i32 0, ptr %s
  br label %loop

loop:
  %iv = load i32, ptr %i
  %cmp = icmp slt i32 %iv, %n
  br i1 %cmp, label %body, label %done

body:
  %ep = getelementptr inbounds i32, ptr %arr, i32 %iv
  %ev = load i32, ptr %ep
  %isPos = icmp sgt i32 %ev, 0
  br i1 %isPos, label %add, label %skip

add:
  %sv = load i32, ptr %s
  %sn = add i32 %sv, %ev
  store i32 %sn, ptr %s
  br label %skip

skip:
  %iv2 = load i32, ptr %i
  %iv3 = add i32 %iv2, 1
  store i32 %iv3, ptr %i
  br label %loop

done:
  %ret = load i32, ptr %s
  ret i32 %ret
}

define i32 @main() {
  %r = call i32 @sum_pos(ptr @nums, i32 5)
  call void @print_num(i32 %r)
  call void @__catvm_int(i8 130)   ; shutdown
  ret i32 0
}
