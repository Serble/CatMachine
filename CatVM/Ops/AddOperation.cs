namespace CatVM.Ops;

public static class AddOperation {
    
    public static void AddRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint left = vm.Cpu.Get(destReg);
        uint right = vm.Cpu.Get(srcReg);
        Add(vm, destReg, left, right);
    }
    
    public static void AddRI(CatVM vm) {
        byte destReg = vm.Read8();
        uint immediate = vm.ReadWord();
        uint left = vm.Cpu.Get(destReg);
        Add(vm, destReg, left, immediate);
    }
    
    public static void Add(CatVM vm, byte destReg, uint a, uint b) {
        uint result = a + b;

        vm.Cpu.OverflowFlag = (~(a ^ b) & (a ^ result)) >> 31 == 1;
        vm.Cpu.ZeroFlag = result == 0;
        vm.Cpu.CarryFlag = result < a || result < b;
        vm.Cpu.SignFlag = (int)result < 0;
        
        vm.Cpu.Set(destReg, result);
    }
}
