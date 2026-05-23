namespace CatVM.Ops;

public static class OrOperation {
    
    public static void OrRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        uint left = vm.Cpu.Get(destReg);
        uint right = vm.Cpu.Get(srcReg);
        uint result = left | right;
        vm.Cpu.Set(destReg, result);
        vm.Cpu.Ip += 3;
    }
    
    public static void OrRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        uint left = vm.Cpu.Get(destReg);
        uint result = left | immediate;
        vm.Cpu.Set(destReg, result);
        vm.Cpu.Ip += 6;
    }
}
