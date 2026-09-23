namespace CatVM.Ops;

public static class NotOperation {
    
    public static void NotR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint value = dest;
        uint result = ~value;
        vm.Cpu.Ip += 2;
        dest = result;
    }
}
