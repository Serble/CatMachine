namespace CatVM.Ops;

public static class NopOperation {

    public static void Nop(CatVm vm) {
        vm.Cpu.Ip += 1;
    }
}
