namespace CatVM.Ops;

public static class NotOperation {
    
    public static void NotR(CatVM vm) {
        byte destReg = vm.ReadByte();
        uint value = vm.Cpu.Get(destReg);
        uint result = ~value;
        vm.Cpu.Set(destReg, result);
    }
}
