using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class CmpOperation {
    
    public static void CmpRR(CatVm vm) {
        byte leftReg = vm.Read8();
        byte rightReg = vm.Read8();
        uint left = vm.Cpu.Get(leftReg);
        uint right = vm.Cpu.Get(rightReg);
        Cmp(vm, left, right);
    }
    
    public static void CmpRI(CatVm vm) {
        byte leftReg = vm.Read8();
        uint immediate = vm.ReadWord();
        uint left = vm.Cpu.Get(leftReg);
        Cmp(vm, left, immediate);
    }
    
    public static void CmpIR(CatVm vm) {
        uint immediate = vm.ReadWord();
        byte rightReg = vm.Read8();
        uint right = vm.Cpu.Get(rightReg);
        Cmp(vm, immediate, right);
    }
    
    public static void CmpII(CatVm vm) {
        uint left = vm.ReadWord();
        uint right = vm.ReadWord();
        Cmp(vm, left, right);
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
