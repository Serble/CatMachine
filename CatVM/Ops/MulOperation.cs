namespace CatVM.Ops;

public static class MulOperation {
    
    public static void MulRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        uint left = vm.Cpu.Get(destReg);
        uint right = vm.Cpu.Get(srcReg);
        uint result = left * right;
        vm.Cpu.Set(destReg, result);
        vm.Cpu.Ip += 3;
    }
    
    public static void MulRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        uint left = vm.Cpu.Get(destReg);
        uint result = left * immediate;
        vm.Cpu.Set(destReg, result);
        vm.Cpu.Ip += 6;
    }
    
    public static void IMulRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        int left = (int)vm.Cpu.Get(destReg);
        int right = (int)vm.Cpu.Get(srcReg);
        int result = left * right;
        vm.Cpu.Set(destReg, (uint)result);
        vm.Cpu.Ip += 3;
    }
    
    public static void IMulRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        int left = (int)vm.Cpu.Get(destReg);
        int right = (int)immediate;
        int result = left * right;
        vm.Cpu.Set(destReg, (uint)result);
        vm.Cpu.Ip += 6;
    }
}
