namespace CatVM.Ops;

public static class IntOperation {
    
    public static void IntR(CatVm vm) {
        byte idReg = vm.Read8(vm.Cpu.Ip + 1);
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        byte intNumber = (byte)(vm.Cpu.Get(idReg) & 0xFF);
        vm.Cpu.Ip += 2;
        vm.Interrupt(intNumber);
    }
    
    public static void IntI(CatVm vm) {
        byte intNumber = vm.Read8(vm.Cpu.Ip + 1);
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.Cpu.Ip += 2;
        vm.Interrupt(intNumber);
    }
    
    public static void Di(CatVm vm) {
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.InterruptsEnabled = false;
        vm.Cpu.Ip += 1;
    }
    
    public static void Ei(CatVm vm) {
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.InterruptsEnabled = true;
        vm.Cpu.Ip += 1;
    }

    // non privileged interrupt instruction
    public static void Syscall(CatVm vm) {
        vm.Cpu.Ip += 1;
        vm.Interrupt(SpecialInterrupts.Syscall);
    }
}
