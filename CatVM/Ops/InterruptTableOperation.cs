namespace CatVM.Ops;

public static class InterruptTableOperation {

    public static void SetItR(CatVm vm) {
        byte reg = vm.Read8();
        
        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.It = vm.Cpu.Get(reg);
    }

    public static void SetItI(CatVm vm) {
        uint imm = vm.ReadWord();

        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.It = imm;
    }

    public static void GetItR(CatVm vm) {
        byte reg = vm.Read8();
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.Cpu.Set(reg, vm.Cpu.It);
    }
}
