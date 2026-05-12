namespace CatVM.Ops;

public static class CpyOperation {

    public static void CpyRR(CatVm vm) {
        byte sourceReg = vm.Read8();
        byte lengthReg = vm.Read8();
        
        uint sourceAddr = vm.Cpu.Get(sourceReg);;
        uint length = vm.Cpu.Get(lengthReg);
        
        Cpy(vm, sourceAddr, length);
    }
    
    public static void CpyRI(CatVm vm) {
        byte sourceReg = vm.Read8();

        uint sourceAddr = vm.Cpu.Get(sourceReg);
        uint length = vm.ReadWord();
        
        Cpy(vm, sourceAddr, length);
    }
    
    public static void CpyIR(CatVm vm) {
        uint sourceAddr = vm.ReadWord();
        byte lengthReg = vm.Read8();
        
        uint length = vm.Cpu.Get(lengthReg);
        
        Cpy(vm, sourceAddr, length);
    }
    
    public static void CpyII(CatVm vm) {
        uint sourceAddr = vm.ReadWord();
        uint length = vm.ReadWord();
        
        Cpy(vm, sourceAddr, length);
    }
    
    private static void Cpy(CatVm vm, uint sourceAddr, uint length) {  // dest is always in R0
        vm.ValidateMemoryRead(sourceAddr, length);
        vm.ValidateMemoryWrite(vm.Cpu.R0, length);
        
        Buffer.BlockCopy(vm.Memory, (int)sourceAddr, vm.Memory, (int)vm.Cpu.R0, (int)length);
    }
}
