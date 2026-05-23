namespace CatVM.Ops;

public static class NotOperation {
    
    public static void NotR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint value = vm.Cpu.Get(destReg);
        uint result = ~value;
        vm.Cpu.Set(destReg, result);
        vm.Cpu.Ip += 2;
    }
}
