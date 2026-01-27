using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class SubOperation {
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint left = vm.Cpu.Get(destReg);
        uint right = vm.Cpu.Get(srcReg);
        Sub(vm, destReg, left, right);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SubRI(CatVM vm) {
        byte destReg = vm.Read8();
        uint immediate = vm.ReadWord();
        uint left = vm.Cpu.Get(destReg);
        Sub(vm, destReg, left, immediate);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sub(CatVM vm, byte destReg, uint a, uint b) {
        uint result = a - b;
        int sResult = (int)result;
        
        vm.Cpu.ZeroFlag = result == 0;
        vm.Cpu.SignFlag = sResult < 0;
        vm.Cpu.OverflowFlag = ((a ^ b) & (a ^ result)) >> 31 == 1;
        vm.Cpu.CarryFlag = a < b;
        
        vm.Cpu.Set(destReg, result);
    }
}
