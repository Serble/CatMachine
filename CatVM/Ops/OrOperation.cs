using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class OrOperation {
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OrRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint left = vm.Cpu.Get(destReg);
        uint right = vm.Cpu.Get(srcReg);
        uint result = left | right;
        vm.Cpu.Set(destReg, result);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OrRI(CatVM vm) {
        byte destReg = vm.Read8();
        uint immediate = vm.ReadWord();
        uint left = vm.Cpu.Get(destReg);
        uint result = left | immediate;
        vm.Cpu.Set(destReg, result);
    }
}
