namespace CatVM.Ops;

public static class OrOperation {
    
    public static void OrRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint left = dest;
        uint right = vm.Cpu.Get(srcReg);
        uint result = left | right;
        vm.Cpu.Ip += 3;
        dest = result;
    }
    
    public static void OrRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint left = dest;
        uint result = left | immediate;
        vm.Cpu.Ip += 6;
        dest = result;
    }
}
