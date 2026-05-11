namespace CatVM.Ops;

public static class TimingOperation {

    public static void UptMs(CatVM vm) {
        long elapsedMs = vm.Fast ? vm.Runtime.ElapsedMilliseconds : vm.TicksPassed / CatVM.PicosecondsPerMillisecond;
        vm.Cpu.R0 = (uint)elapsedMs;             // low
        vm.Cpu.R1 = (uint)(elapsedMs >> 32);     // high
    }

    public static void UptNs(CatVM vm) {
        long elapsedNs = vm.Fast ? (long)vm.Runtime.Elapsed.TotalNanoseconds : vm.TicksPassed / CatVM.PicosecondsPerNanosecond;
        vm.Cpu.R0 = (uint)elapsedNs;
        vm.Cpu.R1 = (uint)(elapsedNs >> 32);
    }
}
