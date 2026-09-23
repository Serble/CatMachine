namespace CatVM.Ops;

public static class DivOperation {

    // div destReg, remReg
    // destReg = destReg / remReg
    // remReg = destReg % remReg
    public static void DivRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte remReg = vm.Read8(vm.Cpu.Ip + 2);
        ref uint quotientDest = ref vm.Cpu.RegRef(destReg);
        ref uint remainderDest = ref vm.Cpu.RegRef(remReg);
        uint dividend = quotientDest;
        uint divisor = remainderDest;
        (uint quotient, uint remainder) = Divide(dividend, divisor);
        vm.Cpu.Ip += 3;
        quotientDest = quotient;
        remainderDest = remainder;
    }
    
    public static void IDivRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte remReg = vm.Read8(vm.Cpu.Ip + 2);
        ref uint quotientDest = ref vm.Cpu.RegRef(destReg);
        ref uint remainderDest = ref vm.Cpu.RegRef(remReg);
        int dividend = (int)quotientDest;
        int divisor = (int)remainderDest;
        (uint quotient, uint remainder) = Divide(dividend, divisor);
        vm.Cpu.Ip += 3;
        quotientDest = quotient;
        remainderDest = remainder;
    }
    
    // TODO: Don't return tuples here for performance reasons
    // investigate first

    private static (uint quotient, uint remainder) Divide(uint dividend, uint divisor) {
        if (dividend == 0) {
            return (0, 0);
        }
        
        return (dividend / divisor, dividend % divisor);
    }
    
    private static (uint quotient, uint remainder) Divide(int dividend, int divisor) {
        if (dividend == 0) {
            return (0, 0);
        }
        
        return ((uint)(dividend / divisor), (uint)(dividend % divisor));
    }
}
