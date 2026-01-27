using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class DivOperation {

    // div destReg, remReg
    // destReg = destReg / remReg
    // remReg = destReg % remReg
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DivRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte remReg = vm.Read8();
        uint dividend = vm.Cpu.Get(destReg);
        uint divisor = vm.Cpu.Get(remReg);
        (uint quotient, uint remainder) = Divide(dividend, divisor);
        vm.Cpu.Set(destReg, quotient);
        vm.Cpu.Set(remReg, remainder);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IDivRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte remReg = vm.Read8();
        int dividend = (int)vm.Cpu.Get(destReg);
        int divisor = (int)vm.Cpu.Get(remReg);
        (uint quotient, uint remainder) = Divide(dividend, divisor);
        vm.Cpu.Set(destReg, quotient);
        vm.Cpu.Set(remReg, remainder);
    }
    
    // TODO: Don't return tuples here for performance reasons
    // investigate first

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (uint quotient, uint remainder) Divide(uint dividend, uint divisor) {
        if (dividend == 0) {
            return (0, 0);
        }
        
        return (dividend / divisor, dividend % divisor);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (uint quotient, uint remainder) Divide(int dividend, int divisor) {
        if (dividend == 0) {
            return (0, 0);
        }
        
        return ((uint)(dividend / divisor), (uint)(dividend % divisor));
    }
}
