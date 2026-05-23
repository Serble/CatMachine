using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class CmpOperation {
    
    public static void CmpRR(CatVm vm) {
        byte leftReg = vm.Read8(vm.Cpu.Ip + 1);
        byte rightReg = vm.Read8(vm.Cpu.Ip + 2);
        uint left = vm.Cpu.Get(leftReg);
        uint right = vm.Cpu.Get(rightReg);
        Cmp(vm, left, right);
        vm.Cpu.Ip += 3;
    }
    
    public static void CmpRI(CatVm vm) {
        byte leftReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        uint left = vm.Cpu.Get(leftReg);
        Cmp(vm, left, immediate);
        vm.Cpu.Ip += 6;
    }
    
    public static void CmpIR(CatVm vm) {
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 1);
        byte rightReg = vm.Read8(vm.Cpu.Ip + 5);
        uint right = vm.Cpu.Get(rightReg);
        Cmp(vm, immediate, right);
        vm.Cpu.Ip += 6;
    }
    
    public static void CmpII(CatVm vm) {
        uint left = vm.ReadWord(vm.Cpu.Ip + 1);
        uint right = vm.ReadWord(vm.Cpu.Ip + 5);
        Cmp(vm, left, right);
        vm.Cpu.Ip += 9;
    }
    
    // inlining performance tests are inconclusive, but seemed somewhat positive.
    // at worst, it doesn't seem to have a negative impact.
    // feel free to retest and remove if needed.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Cmp(CatVm vm, uint a, uint b) {
        uint result = a - b;
        int sResult = (int)result;
        
        vm.Cpu.ZeroFlag = result == 0;
        vm.Cpu.SignFlag = sResult < 0;
        vm.Cpu.OverflowFlag = ((a ^ b) & (a ^ result)) >> 31 == 1;
        vm.Cpu.CarryFlag = a < b;
    }
}
