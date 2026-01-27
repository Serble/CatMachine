using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class NotOperation {
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void NotR(CatVM vm) {
        byte destReg = vm.Read8();
        uint value = vm.Cpu.Get(destReg);
        uint result = ~value;
        vm.Cpu.Set(destReg, result);
    }
}
