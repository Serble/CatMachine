namespace CatVM.Ops;

public static class InterruptTableOperation {

    public static void SetItR(CatVm vm) {
        byte reg = vm.Read8(vm.Cpu.Ip + 1);
        
        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.It = vm.Cpu.Get(reg);
        vm.Cpu.Ip += 2;
    }

    public static void SetItI(CatVm vm) {
        uint imm = vm.ReadWord(vm.Cpu.Ip + 1);

        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.It = imm;
        vm.Cpu.Ip += 5;
    }

    public static void GetItR(CatVm vm) {
        byte reg = vm.Read8(vm.Cpu.Ip + 1);
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.Cpu.Set(reg, vm.Cpu.It);
        vm.Cpu.Ip += 2;
    }
}
