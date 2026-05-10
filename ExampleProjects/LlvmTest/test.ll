; ModuleID = 'test.c'
source_filename = "test.c"
target datalayout = "e-m:e-p:32:32-p270:32:32-p271:32:32-p272:64:64-i128:128-f64:32:64-f80:32-n8:16:32-S128"
target triple = "i386-unknown-none"

@.str = private unnamed_addr constant [18 x i8] c"Hello from CatVM!\00", align 1
@.str.1 = private unnamed_addr constant [14 x i8] c"fib(0..10) = \00", align 1
@.str.2 = private unnamed_addr constant [13 x i8] c"uptime ms = \00", align 1
@.str.3 = private unnamed_addr constant [14 x i8] c"0xdeadbeef = \00", align 1
@.str.4 = private unnamed_addr constant [4 x i8] c"cat\00", align 1
@.str.5 = private unnamed_addr constant [26 x i8] c"strcmp(cat,cat) == 0   ok\00", align 1
@.str.6 = private unnamed_addr constant [6 x i8] c"apple\00", align 1
@.str.7 = private unnamed_addr constant [7 x i8] c"banana\00", align 1
@.str.8 = private unnamed_addr constant [28 x i8] c"strcmp(apple,banana) < 0 ok\00", align 1
@.str.9 = private unnamed_addr constant [6 x i8] c"zebra\00", align 1
@.str.10 = private unnamed_addr constant [27 x i8] c"strcmp(zebra,apple) > 0 ok\00", align 1
@.str.11 = private unnamed_addr constant [14 x i8] c"memset 5 X: '\00", align 1
@.str.12 = private unnamed_addr constant [2 x i8] c"'\00", align 1
@.str.13 = private unnamed_addr constant [6 x i8] c"hello\00", align 1
@.str.14 = private unnamed_addr constant [16 x i8] c"memcpy hello: '\00", align 1
@.str.15 = private unnamed_addr constant [19 x i8] c"strlen('hello') = \00", align 1
@.str.16 = private unnamed_addr constant [5 x i8] c"bye!\00", align 1

; Function Attrs: noinline nounwind optnone
define dso_local i32 @main() #0 {
  %1 = alloca i32, align 4
  %2 = alloca i32, align 4
  %3 = alloca i32, align 4
  %4 = alloca i32, align 4
  %5 = alloca [16 x i8], align 1
  call void @puts(ptr noundef @.str) #2
  call void @puts_raw(ptr noundef @.str.1) #2
  store i32 0, ptr %1, align 4
  store i32 1, ptr %2, align 4
  store i32 0, ptr %3, align 4
  br label %6

6:                                                ; preds = %16, %0
  %7 = load i32, ptr %3, align 4
  %8 = icmp sle i32 %7, 10
  br i1 %8, label %9, label %19

9:                                                ; preds = %6
  %10 = load i32, ptr %1, align 4
  call void @puti(i32 noundef %10) #2
  call void @putchar(i8 noundef signext 32) #2
  %11 = load i32, ptr %1, align 4
  %12 = load i32, ptr %2, align 4
  %13 = add nsw i32 %11, %12
  store i32 %13, ptr %4, align 4
  %14 = load i32, ptr %1, align 4
  store i32 %14, ptr %2, align 4
  %15 = load i32, ptr %4, align 4
  store i32 %15, ptr %1, align 4
  br label %16

16:                                               ; preds = %9
  %17 = load i32, ptr %3, align 4
  %18 = add nsw i32 %17, 1
  store i32 %18, ptr %3, align 4
  br label %6, !llvm.loop !4

19:                                               ; preds = %6
  call void @putchar(i8 noundef signext 10) #2
  call void @puts_raw(ptr noundef @.str.2) #2
  %20 = call i32 @uptime_ms() #2
  call void @putu(i32 noundef %20) #2
  call void @putchar(i8 noundef signext 10) #2
  call void @puts_raw(ptr noundef @.str.3) #2
  call void @putx(i32 noundef -559038737) #2
  call void @putchar(i8 noundef signext 10) #2
  %21 = call i32 @strcmp(ptr noundef @.str.4, ptr noundef @.str.4) #2
  %22 = icmp eq i32 %21, 0
  br i1 %22, label %23, label %24

23:                                               ; preds = %19
  call void @puts(ptr noundef @.str.5) #2
  br label %24

24:                                               ; preds = %23, %19
  %25 = call i32 @strcmp(ptr noundef @.str.6, ptr noundef @.str.7) #2
  %26 = icmp slt i32 %25, 0
  br i1 %26, label %27, label %28

27:                                               ; preds = %24
  call void @puts(ptr noundef @.str.8) #2
  br label %28

28:                                               ; preds = %27, %24
  %29 = call i32 @strcmp(ptr noundef @.str.9, ptr noundef @.str.6) #2
  %30 = icmp sgt i32 %29, 0
  br i1 %30, label %31, label %32

31:                                               ; preds = %28
  call void @puts(ptr noundef @.str.10) #2
  br label %32

32:                                               ; preds = %31, %28
  %33 = getelementptr inbounds [16 x i8], ptr %5, i32 0, i32 0
  %34 = call ptr @memset(ptr noundef %33, i32 noundef 88, i32 noundef 5) #2
  %35 = getelementptr inbounds [16 x i8], ptr %5, i32 0, i32 5
  store i8 0, ptr %35, align 1
  call void @puts_raw(ptr noundef @.str.11) #2
  %36 = getelementptr inbounds [16 x i8], ptr %5, i32 0, i32 0
  call void @puts_raw(ptr noundef %36) #2
  call void @puts(ptr noundef @.str.12) #2
  %37 = getelementptr inbounds [16 x i8], ptr %5, i32 0, i32 0
  %38 = call ptr @memcpy(ptr noundef %37, ptr noundef @.str.13, i32 noundef 6) #2
  call void @puts_raw(ptr noundef @.str.14) #2
  %39 = getelementptr inbounds [16 x i8], ptr %5, i32 0, i32 0
  call void @puts_raw(ptr noundef %39) #2
  call void @puts(ptr noundef @.str.12) #2
  call void @puts_raw(ptr noundef @.str.15) #2
  %40 = call i32 @strlen(ptr noundef @.str.13) #2
  call void @putu(i32 noundef %40) #2
  call void @putchar(i8 noundef signext 10) #2
  call void @puts(ptr noundef @.str.16) #2
  call void @exit(i32 noundef 0) #2
  ret i32 0
}

declare dso_local void @puts(ptr noundef) #1

declare dso_local void @puts_raw(ptr noundef) #1

declare dso_local void @puti(i32 noundef) #1

declare dso_local void @putchar(i8 noundef signext) #1

declare dso_local void @putu(i32 noundef) #1

declare dso_local i32 @uptime_ms() #1

declare dso_local void @putx(i32 noundef) #1

declare dso_local i32 @strcmp(ptr noundef, ptr noundef) #1

declare dso_local ptr @memset(ptr noundef, i32 noundef, i32 noundef) #1

declare dso_local ptr @memcpy(ptr noundef, ptr noundef, i32 noundef) #1

declare dso_local i32 @strlen(ptr noundef) #1

declare dso_local void @exit(i32 noundef) #1

attributes #0 = { noinline nounwind optnone "frame-pointer"="all" "min-legal-vector-width"="0" "no-builtins" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="pentium4" "target-features"="+cmov,+cx8,+fxsr,+mmx,+sse,+sse2,+x87" "tune-cpu"="generic" }
attributes #1 = { "frame-pointer"="all" "no-builtins" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="pentium4" "target-features"="+cmov,+cx8,+fxsr,+mmx,+sse,+sse2,+x87" "tune-cpu"="generic" }
attributes #2 = { nobuiltin "no-builtins" }

!llvm.module.flags = !{!0, !1, !2}
!llvm.ident = !{!3}

!0 = !{i32 1, !"NumRegisterParameters", i32 0}
!1 = !{i32 1, !"wchar_size", i32 4}
!2 = !{i32 7, !"frame-pointer", i32 2}
!3 = !{!"clang version 21.1.8 (Fedora 21.1.8-4.fc43)"}
!4 = distinct !{!4, !5}
!5 = !{!"llvm.loop.mustprogress"}
