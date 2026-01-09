namespace CatVM.Ops;

public static class IntOperation {
    
    public static void IntR(CatVM vm) {
        byte idReg = vm.Read8();
        byte intNumber = (byte)(vm.Cpu.Get(idReg) & 0xFF);
        vm.Interrupt(intNumber);
    }
    
    public static void IntI(CatVM vm) {
        byte intNumber = vm.Read8();
        vm.Interrupt(intNumber);
    }
    
    public static void Di(CatVM vm) {
        vm.InterruptsEnabled = false;
    }
    
    public static void Ei(CatVM vm) {
        vm.InterruptsEnabled = true;
    }
}
