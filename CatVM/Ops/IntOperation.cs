using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class IntOperation {
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IntR(CatVM vm) {
        byte idReg = vm.Read8();
        byte intNumber = (byte)(vm.Cpu.Get(idReg) & 0xFF);
        vm.Interrupt(intNumber);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IntI(CatVM vm) {
        byte intNumber = vm.Read8();
        vm.Interrupt(intNumber);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Di(CatVM vm) {
        vm.InterruptsEnabled = false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Ei(CatVM vm) {
        vm.InterruptsEnabled = true;
    }
}
