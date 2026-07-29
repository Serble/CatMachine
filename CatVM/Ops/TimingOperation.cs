namespace CatVM.Ops;

public static class TimingOperation {

    public static void UptMs(CatVm vm) {
        long elapsedMs = vm.Fast ? vm.Runtime.ElapsedMilliseconds : vm.TicksPassed / CatVm.PicosecondsPerMillisecond;
        vm.Cpu.R0 = (uint)elapsedMs;             // low
        // Stryker disable once bitwise: elapsedMs is non-negative, so >> and >>> are identical here
        vm.Cpu.R1 = (uint)(elapsedMs >> 32);     // high
        vm.Cpu.Ip += 1;
    }

    public static void UptNs(CatVm vm) {
        long elapsedNs = vm.Fast ? (long)vm.Runtime.Elapsed.TotalNanoseconds : vm.TicksPassed / CatVm.PicosecondsPerNanosecond;
        vm.Cpu.R0 = (uint)elapsedNs;
        // Stryker disable once bitwise: elapsedNs is non-negative, so >> and >>> are identical here
        vm.Cpu.R1 = (uint)(elapsedNs >> 32);
        vm.Cpu.Ip += 1;
    }
}
