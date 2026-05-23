namespace CatVM.Ops;

public static class AddOperation {
    
    public static void AddRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        uint left = vm.Cpu.Get(destReg);
        uint right = vm.Cpu.Get(srcReg);
        Add(vm, destReg, left, right);
        vm.Cpu.Ip += 3;
    }
    
    public static void AddRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        uint left = vm.Cpu.Get(destReg);
        Add(vm, destReg, left, immediate);
        vm.Cpu.Ip += 6;
    }
    
    public static void Add(CatVm vm, byte destReg, uint a, uint b) {
        uint result = a + b;

        vm.Cpu.OverflowFlag = (~(a ^ b) & (a ^ result)) >> 31 == 1;
        vm.Cpu.ZeroFlag = result == 0;
        vm.Cpu.CarryFlag = result < a || result < b;
        vm.Cpu.SignFlag = (int)result < 0;
        
        vm.Cpu.Set(destReg, result);
    }
}
