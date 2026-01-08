namespace CatVM.Ops;

public static class SubOperation {
    
    public static void SubRR(CatVM vm) {
        byte destReg = vm.ReadByte();
        byte srcReg = vm.ReadByte();
        uint left = vm.Cpu.Get(destReg);
        uint right = vm.Cpu.Get(srcReg);
        Sub(vm, destReg, left, right);
    }
    
    public static void SubRI(CatVM vm) {
        byte destReg = vm.ReadByte();
        uint immediate = vm.ReadWord();
        uint left = vm.Cpu.Get(destReg);
        Sub(vm, destReg, left, immediate);
    }

    public static void Sub(CatVM vm, byte destReg, uint a, uint b) {
        uint result = a - b;
        int sResult = (int)result;
        
        vm.Cpu.ZeroFlag = result == 0;
        vm.Cpu.SignFlag = sResult < 0;
        vm.Cpu.OverflowFlag = ((a ^ b) & (a ^ result)) >> 31 == 1;
        vm.Cpu.CarryFlag = a < b;
        
        vm.Cpu.Set(destReg, result);
    }
}
