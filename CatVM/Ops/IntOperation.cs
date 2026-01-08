namespace CatVM.Ops;

public static class IntOperation {
    
    public static void IntR(CatVM vm) {
        byte idReg = vm.ReadByte();
        byte intNumber = (byte)(vm.Cpu.Get(idReg) & 0xFF);
        vm.Interrupt(intNumber);
    }
    
    public static void IntI(CatVM vm) {
        uint intNumber = vm.ReadWord();
        vm.Interrupt((byte)(intNumber & 0xFF));
    }
}
