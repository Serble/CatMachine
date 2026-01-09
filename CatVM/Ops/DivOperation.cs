namespace CatVM.Ops;

public static class DivOperation {

    // div destReg, remReg
    // destReg = destReg / remReg
    // remReg = destReg % remReg
    public static void DivRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte remReg = vm.Read8();
        uint dividend = vm.Cpu.Get(destReg);
        uint divisor = vm.Cpu.Get(remReg);
        uint quotient = dividend / divisor;
        uint remainder = dividend % divisor;
        vm.Cpu.Set(destReg, quotient);
        vm.Cpu.Set(remReg, remainder);
    }
    
    public static void IDivRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte remReg = vm.Read8();
        int dividend = (int)vm.Cpu.Get(destReg);
        int divisor = (int)vm.Cpu.Get(remReg);
        int quotient = dividend / divisor;
        int remainder = dividend % divisor;
        vm.Cpu.Set(destReg, (uint)quotient);
        vm.Cpu.Set(remReg, (uint)remainder);
    }
}
