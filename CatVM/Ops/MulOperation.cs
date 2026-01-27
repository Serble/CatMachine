using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class MulOperation {
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MulRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint left = vm.Cpu.Get(destReg);
        uint right = vm.Cpu.Get(srcReg);
        uint result = left * right;
        vm.Cpu.Set(destReg, result);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MulRI(CatVM vm) {
        byte destReg = vm.Read8();
        uint immediate = vm.ReadWord();
        uint left = vm.Cpu.Get(destReg);
        uint result = left * immediate;
        vm.Cpu.Set(destReg, result);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IMulRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte srcReg = vm.Read8();
        int left = (int)vm.Cpu.Get(destReg);
        int right = (int)vm.Cpu.Get(srcReg);
        int result = left * right;
        vm.Cpu.Set(destReg, (uint)result);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IMulRI(CatVM vm) {
        byte destReg = vm.Read8();
        uint immediate = vm.ReadWord();
        int left = (int)vm.Cpu.Get(destReg);
        int right = (int)immediate;
        int result = left * right;
        vm.Cpu.Set(destReg, (uint)result);
    }
}
