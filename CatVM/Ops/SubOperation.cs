namespace CatVM.Ops;

public static class SubOperation {
    
    public static void SubRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint left = dest;
        uint right = vm.Cpu.Get(srcReg);
        uint result = Sub(vm, left, right);
        vm.Cpu.Ip += 3;
        dest = result;
    }
    
    public static void SubRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint left = dest;
        uint result = Sub(vm, left, immediate);
        vm.Cpu.Ip += 6;
        dest = result;
    }

    public static uint Sub(CatVm vm, uint a, uint b) {
        uint result = a - b;
        int sResult = (int)result;
        
        vm.Cpu.ZeroFlag = result == 0;
        vm.Cpu.SignFlag = sResult < 0;
        vm.Cpu.OverflowFlag = ((a ^ b) & (a ^ result)) >> 31 == 1;
        vm.Cpu.CarryFlag = a < b;
        
        return result;
    }
}
