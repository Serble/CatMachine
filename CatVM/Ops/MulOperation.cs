namespace CatVM.Ops;

public static class MulOperation {
    
    public static void MulRR(CatVM vm) {
        byte destReg = vm.ReadByte();
        byte srcReg = vm.ReadByte();
        uint left = vm.Cpu.Get(destReg);
        uint right = vm.Cpu.Get(srcReg);
        uint result = left * right;
        vm.Cpu.Set(destReg, result);
    }
    
    public static void MulRI(CatVM vm) {
        byte destReg = vm.ReadByte();
        uint immediate = vm.ReadWord();
        uint left = vm.Cpu.Get(destReg);
        uint result = left * immediate;
        vm.Cpu.Set(destReg, result);
    }
    
    public static void IMulRR(CatVM vm) {
        byte destReg = vm.ReadByte();
        byte srcReg = vm.ReadByte();
        int left = (int)vm.Cpu.Get(destReg);
        int right = (int)vm.Cpu.Get(srcReg);
        int result = left * right;
        vm.Cpu.Set(destReg, (uint)result);
    }
    
    public static void IMulRI(CatVM vm) {
        byte destReg = vm.ReadByte();
        uint immediate = vm.ReadWord();
        int left = (int)vm.Cpu.Get(destReg);
        int right = (int)immediate;
        int result = left * right;
        vm.Cpu.Set(destReg, (uint)result);
    }
}
