using CatVM.AotTest.Tests;

namespace CatVM.AotTest;

/// <summary>
/// Standalone Native-AOT test harness for the Cat VM. Every part of the VM is exercised
/// here so that publishing this project with <c>PublishAot=true</c> proves the VM runs
/// correctly as a trimmed, ahead-of-time-compiled binary with no reflection or JIT.
/// </summary>
public static class Program {
    public static int Main() {
        Console.WriteLine("CatVM Native AOT test harness");
        Console.WriteLine("=============================");

        TestRunner runner = new();

        CpuStateTests.Register(runner);
        CoreTests.Register(runner);
        MovTests.Register(runner);
        ArithmeticTests.Register(runner);
        LogicTests.Register(runner);
        ControlFlowTests.Register(runner);
        StackTests.Register(runner);
        SerialTests.Register(runner);
        InterruptTests.Register(runner);

        int failures = runner.RunAll();
        return failures == 0 ? 0 : 1;
    }
}
