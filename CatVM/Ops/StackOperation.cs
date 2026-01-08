namespace CatVM.Ops;

public static class StackOperation {
    
    public static void PushR(CatVM vm) {
        byte srcReg = vm.ReadByte();
        uint value = vm.Cpu.Get(srcReg);
        vm.StackPush(value);
    }
    
    public static void PushI(CatVM vm) {
        uint immediate = vm.ReadWord();
        vm.StackPush(immediate);
    }
    
    public static void PopR(CatVM vm) {
        byte destReg = vm.ReadByte();
        uint value = vm.StackPop();
        vm.Cpu.Set(destReg, value);
    }
    
    public static void Call(CatVM vm) {
        byte addressReg = vm.ReadByte();
        uint offset = vm.ReadWord();
        vm.StackPush(vm.Cpu.Ip);
        JmpOperation.Jmp(vm, addressReg, offset);
    }
    
    public static void Ret(CatVM vm) {
        uint returnAddress = vm.StackPop();
        vm.Cpu.Ip = returnAddress;
    }
}
