namespace CatVM.Ops;

public static class CpyOperation {

    public static void CpyRR(CatVM vm) {
        byte sourceReg = vm.ReadByte();
        byte lengthReg = vm.ReadByte();
        
        uint sourceAddr = vm.Cpu.Get(sourceReg);;
        uint length = vm.Cpu.Get(lengthReg);
        
        Cpy(vm, sourceAddr, length);
    }
    
    public static void CpyRI(CatVM vm) {
        byte sourceReg = vm.ReadByte();

        uint sourceAddr = vm.Cpu.Get(sourceReg);
        uint length = vm.ReadWord();
        
        Cpy(vm, sourceAddr, length);
    }
    
    public static void CpyIR(CatVM vm) {
        uint sourceAddr = vm.ReadWord();
        byte lengthReg = vm.ReadByte();
        
        uint length = vm.Cpu.Get(lengthReg);
        
        Cpy(vm, sourceAddr, length);
    }
    
    public static void CpyII(CatVM vm) {
        uint sourceAddr = vm.ReadWord();
        uint length = vm.ReadWord();
        
        Cpy(vm, sourceAddr, length);
    }
    
    private static void Cpy(CatVM vm, uint sourceAddr, uint length) {
        Buffer.BlockCopy(vm.Memory, (int)sourceAddr, vm.Memory, (int)vm.Cpu.R0, (int)length);
    }
}
