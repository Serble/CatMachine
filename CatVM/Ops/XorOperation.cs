namespace CatVM.Ops;

public static class XorOperation {
    
    public static void XorRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint left = vm.Cpu.Get(destReg);
        uint right = vm.Cpu.Get(srcReg);
        uint result = left ^ right;
        vm.Cpu.Set(destReg, result);
    }
    
    public static void XorRI(CatVM vm) {
        byte destReg = vm.Read8();
        uint immediate = vm.ReadWord();
        uint left = vm.Cpu.Get(destReg);
        uint result = left ^ immediate;
        vm.Cpu.Set(destReg, result);
    }
}
