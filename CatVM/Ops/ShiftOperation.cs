namespace CatVM.Ops;

public static class ShiftOperation {
    
    public static void ShlRR(CatVm vm) {
        byte destReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint left = vm.Cpu.Get(destReg);
        uint right = vm.Cpu.Get(srcReg);
        uint result = left << (int)right;
        vm.Cpu.Set(destReg, result);
    }
    
    public static void ShlRI(CatVm vm) {
        byte destReg = vm.Read8();
        uint immediate = vm.ReadWord();
        uint left = vm.Cpu.Get(destReg);
        uint result = left << (int)immediate;
        vm.Cpu.Set(destReg, result);
    }
    
    public static void ShrRR(CatVm vm) {
        byte destReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint left = vm.Cpu.Get(destReg);
        uint right = vm.Cpu.Get(srcReg);
        uint result = left >> (int)right;
        vm.Cpu.Set(destReg, result);
    }
    
    public static void ShrRI(CatVm vm) {
        byte destReg = vm.Read8();
        uint immediate = vm.ReadWord();
        uint left = vm.Cpu.Get(destReg);
        uint result = left >> (int)immediate;
        vm.Cpu.Set(destReg, result);
    }
}