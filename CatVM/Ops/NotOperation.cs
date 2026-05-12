namespace CatVM.Ops;

public static class NotOperation {
    
    public static void NotR(CatVm vm) {
        byte destReg = vm.Read8();
        uint value = vm.Cpu.Get(destReg);
        uint result = ~value;
        vm.Cpu.Set(destReg, result);
    }
}
