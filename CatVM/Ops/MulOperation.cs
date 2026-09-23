namespace CatVM.Ops;

public static class MulOperation {
    
    public static void MulRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint left = dest;
        uint right = vm.Cpu.Get(srcReg);
        uint result = left * right;
        vm.Cpu.Ip += 3;
        dest = result;
    }
    
    public static void MulRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint left = dest;
        uint result = left * immediate;
        vm.Cpu.Ip += 6;
        dest = result;
    }
    
    public static void IMulRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        int left = (int)dest;
        int right = (int)vm.Cpu.Get(srcReg);
        int result = left * right;
        vm.Cpu.Ip += 3;
        dest = (uint)result;
    }
    
    public static void IMulRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        int left = (int)dest;
        int right = (int)immediate;
        int result = left * right;
        vm.Cpu.Ip += 6;
        dest = (uint)result;
    }
}
