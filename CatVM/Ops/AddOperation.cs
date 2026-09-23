namespace CatVM.Ops;

public static class AddOperation {
    
    public static void AddRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint left = dest;
        uint right = vm.Cpu.Get(srcReg);
        uint result = Add(vm, left, right);
        vm.Cpu.Ip += 3;
        dest = result;
    }
    
    public static void AddRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint left = dest;
        uint result = Add(vm, left, immediate);
        vm.Cpu.Ip += 6;
        dest = result;
    }
    
    public static uint Add(CatVm vm, uint a, uint b) {
        uint result = a + b;

        vm.Cpu.OverflowFlag = (~(a ^ b) & (a ^ result)) >> 31 == 1;
        vm.Cpu.ZeroFlag = result == 0;
        vm.Cpu.CarryFlag = result < a || result < b;
        vm.Cpu.SignFlag = (int)result < 0;
        
        return result;
    }
}
