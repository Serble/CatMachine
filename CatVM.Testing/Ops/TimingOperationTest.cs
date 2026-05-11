using CatVM.Ops;

namespace CatVM.Testing.Ops;

/// <summary>
/// Tests for <c>uptms</c> and <c>uptns</c> opcodes (<see cref="TimingOperation"/>).
/// </summary>
public class TimingOperationTest : OperationTestBase {
    private const byte OpUptMs = 0x5A;
    private const byte OpUptNs = 0x5B;

    [Test]
    public void UptMs_AdvancesIpByOne() {
        Execute(OpUptMs);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(1u));
    }

    [Test]
    public void UptNs_AdvancesIpByOne() {
        Execute(OpUptNs);
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(1u));
    }

    [Test]
    public void UptMs_VirtualMode_WritesElapsedMillisecondsFromTicks() {
        CatVM vm = new(512, 10_000) { Fast = false };
        // Burn a known amount of virtual time first by executing some NOPs.
        // 1 NOP = 1 cycle; PicosecondsPerCycle depends on cyclesPerSecond.
        vm.LoadData([0x4D, 0x4D, 0x4D, OpUptMs]); // 3 NOPs, then UPTMS
        vm.Cpu.Ip = 0;
        vm.ExecuteInstruction(true);
        vm.ExecuteInstruction(true);
        vm.ExecuteInstruction(true);
        long ticksBefore = vm.TicksPassed;
        vm.ExecuteInstruction(true);

        long expectedMs = ticksBefore / CatVM.PicosecondsPerMillisecond;
        long actual = ((long)vm.Cpu.R1 << 32) | vm.Cpu.R0;
        Assert.That(actual, Is.EqualTo(expectedMs));
    }

    [Test]
    public void UptNs_VirtualMode_WritesElapsedNanosecondsFromTicks() {
        CatVM vm = new(512, 10_000) { Fast = false };
        vm.LoadData([0x4D, 0x4D, 0x4D, OpUptNs]);
        vm.Cpu.Ip = 0;
        vm.ExecuteInstruction(true);
        vm.ExecuteInstruction(true);
        vm.ExecuteInstruction(true);
        long ticksBefore = vm.TicksPassed;
        vm.ExecuteInstruction(true);

        long expectedNs = ticksBefore / CatVM.PicosecondsPerNanosecond;
        long actual = ((long)vm.Cpu.R1 << 32) | vm.Cpu.R0;
        Assert.That(actual, Is.EqualTo(expectedNs));
    }

    [Test]
    public void UptMs_FastMode_UsesRuntimeStopwatch() {
        CatVM vm = new(512, 10_000) { Fast = true };
        vm.LoadData([OpUptMs]);
        vm.Cpu.Ip = 0;
        vm.ExecuteInstruction(true);

        long actual = ((long)vm.Cpu.R1 << 32) | vm.Cpu.R0;
        // Runtime stopwatch is started on Reset(); the value is small but non-negative.
        Assert.That(actual, Is.GreaterThanOrEqualTo(0L));
        Assert.That(actual, Is.LessThan(60_000L)); // sanity bound: under a minute.
    }

    [Test]
    public void UptNs_FastMode_UsesRuntimeStopwatch() {
        CatVM vm = new(512, 10_000) { Fast = true };
        vm.LoadData([OpUptNs]);
        vm.Cpu.Ip = 0;
        vm.ExecuteInstruction(true);

        long actual = ((long)vm.Cpu.R1 << 32) | vm.Cpu.R0;
        Assert.That(actual, Is.GreaterThanOrEqualTo(0L));
        // Sanity bound: under a minute in nanoseconds.
        Assert.That(actual, Is.LessThan(60_000_000_000L));
    }

    [Test]
    public void UptMs_DirectCall_SplitsLowAndHighWords() {
        // Drive the operation directly so we can pick an exact tick value.
        // ms = 0x1_0000_0001 -> low = 1, high = 1.
        long picosForMs = 0x1_0000_0001L * CatVM.PicosecondsPerMillisecond;
        SetTicksPassed(_vm, picosForMs);

        TimingOperation.UptMs(_vm);

        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R0, Is.EqualTo(1u));
            Assert.That(_vm.Cpu.R1, Is.EqualTo(1u));
        });
    }

    [Test]
    public void UptNs_DirectCall_SplitsLowAndHighWords() {
        long picosForNs = 0x1_0000_0002L * CatVM.PicosecondsPerNanosecond;
        SetTicksPassed(_vm, picosForNs);

        TimingOperation.UptNs(_vm);

        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R0, Is.EqualTo(2u));
            Assert.That(_vm.Cpu.R1, Is.EqualTo(1u));
        });
    }

    private static void SetTicksPassed(CatVM vm, long value) {
        // TicksPassed has a private setter; nudge it via reflection so we can
        // assert exact bit-splitting behaviour without timing flakiness.
        System.Reflection.PropertyInfo prop =
            typeof(CatVM).GetProperty(nameof(CatVM.TicksPassed))!;
        prop.SetValue(vm, value);
    }
}
