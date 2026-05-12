namespace CatVM.Ops;

public static class IntOperation {
    
    public static void IntR(CatVm vm) {
        byte idReg = vm.Read8();
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        byte intNumber = (byte)(vm.Cpu.Get(idReg) & 0xFF);
        vm.Interrupt(intNumber);
    }
    
    public static void IntI(CatVm vm) {
        byte intNumber = vm.Read8();
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.Interrupt(intNumber);
    }
    
    public static void Di(CatVm vm) {
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.InterruptsEnabled = false;
    }
    
    public static void Ei(CatVm vm) {
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.InterruptsEnabled = true;
    }

    // non privileged interrupt instruction
    public static void Syscall(CatVm vm) {
        vm.Interrupt(SpecialInterrupts.Syscall);
    }
}
