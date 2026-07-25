namespace CatVM.Ops;

public static class CpyOperation {

    public static void CpyRR(CatVm vm) {
        byte sourceReg = vm.Read8(vm.Cpu.Ip + 1);
        byte lengthReg = vm.Read8(vm.Cpu.Ip + 2);
        
        uint sourceAddr = vm.Cpu.Get(sourceReg);;
        uint length = vm.Cpu.Get(lengthReg);
        
        Cpy(vm, sourceAddr, length);
        vm.Cpu.Ip += 3;
    }
    
    public static void CpyRI(CatVm vm) {
        byte sourceReg = vm.Read8(vm.Cpu.Ip + 1);

        uint sourceAddr = vm.Cpu.Get(sourceReg);
        uint length = vm.ReadWord(vm.Cpu.Ip + 2);
        
        Cpy(vm, sourceAddr, length);
        vm.Cpu.Ip += 6;
    }
    
    public static void CpyIR(CatVm vm) {
        uint sourceAddr = vm.ReadWord(vm.Cpu.Ip + 1);
        byte lengthReg = vm.Read8(vm.Cpu.Ip + 5);
        
        uint length = vm.Cpu.Get(lengthReg);
        
        Cpy(vm, sourceAddr, length);
        vm.Cpu.Ip += 6;
    }
    
    public static void CpyII(CatVm vm) {
        uint sourceAddr = vm.ReadWord(vm.Cpu.Ip + 1);
        uint length = vm.ReadWord(vm.Cpu.Ip + 5);
        
        Cpy(vm, sourceAddr, length);
        vm.Cpu.Ip += 9;
    }
    
    private static void Cpy(CatVm vm, uint sourceAddr, uint length) {  // dest is always in R0
        uint srcPhys = vm.Translate(sourceAddr, length);
        uint dstPhys = vm.Translate(vm.Cpu.R0, length);
        vm.ValidateMemoryRead(srcPhys, length);
        vm.ValidateMemoryWrite(dstPhys, length);

        Buffer.BlockCopy(vm.Memory, (int)srcPhys, vm.Memory, (int)dstPhys, (int)length);
    }
}
