namespace CatVM.Ops;

public static class ShiftOperation {

    public static void ShlRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint left = dest;
        uint right = vm.Cpu.Get(srcReg);
        uint result = left << (int)right;
        vm.Cpu.Ip += 3;
        dest = result;
    }

    public static void ShlRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint left = dest;
        uint result = left << (int)immediate;
        vm.Cpu.Ip += 6;
        dest = result;
    }

    public static void ShrRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint left = dest;
        uint right = vm.Cpu.Get(srcReg);
        // Stryker disable once bitwise: left is unsigned, so >> and >>> are identical here
        uint result = left >> (int)right;
        vm.Cpu.Ip += 3;
        dest = result;
    }

    public static void ShrRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint left = dest;
        // Stryker disable once bitwise: left is unsigned, so >> and >>> are identical here
        uint result = left >> (int)immediate;
        vm.Cpu.Ip += 6;
        dest = result;
    }
}
