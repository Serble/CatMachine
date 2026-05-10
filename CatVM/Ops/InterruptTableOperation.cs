namespace CatVM.Ops;

public static class InterruptTableOperation {

    public static void SetItR(CatVM vm) {
        byte reg = vm.Read8();
        
        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.It = vm.Cpu.Get(reg);
    }

    public static void SetItI(CatVM vm) {
        uint imm = vm.ReadWord();

        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.It = imm;
    }

    public static void GetItR(CatVM vm) {
        byte reg = vm.Read8();
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.Cpu.Set(reg, vm.Cpu.It);
    }
}
