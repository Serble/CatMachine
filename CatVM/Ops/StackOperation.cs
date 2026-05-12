namespace CatVM.Ops;

public static class StackOperation {
    
    public static void PushR(CatVm vm) {
        byte srcReg = vm.Read8();
        uint value = vm.Cpu.Get(srcReg);
        vm.StackPush(value);
    }
    
    public static void PushI(CatVm vm) {
        uint immediate = vm.ReadWord();
        vm.StackPush(immediate);
    }
    
    public static void Push8R(CatVm vm) {
        byte srcReg = vm.Read8();
        byte value = (byte)(vm.Cpu.Get(srcReg) & 0xFF);
        vm.StackPush(value);
    }
    
    public static void Push8I(CatVm vm) {
        byte immediate = vm.Read8();
        vm.StackPush(immediate);
    }
    
    public static void Push16R(CatVm vm) {
        byte srcReg = vm.Read8();
        ushort value = (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF);
        vm.StackPush(value);
    }
    
    public static void Push16I(CatVm vm) {
        ushort immediate = vm.Read16();
        vm.StackPush(immediate);
    }
    
    public static void PopR(CatVm vm) {
        byte destReg = vm.Read8();
        uint value = vm.StackPop();
        vm.Cpu.Set(destReg, value);
    }
    
    public static void Pop8R(CatVm vm) {
        byte destReg = vm.Read8();
        byte value = vm.StackPop8();
        vm.Cpu.Set(destReg, value);
    }
    
    public static void Pop16R(CatVm vm) {
        byte destReg = vm.Read8();
        ushort value = vm.StackPop16();
        vm.Cpu.Set(destReg, value);
    }
    
    public static void Call(CatVm vm) {
        byte addressReg = vm.Read8();
        uint offset = vm.ReadWord();
        vm.StackPush(vm.Cpu.Ip);
        JmpOperation.Jmp(vm, addressReg, offset);
    }
    
    public static void Ret(CatVm vm) {
        uint returnAddress = vm.StackPop();
        vm.Cpu.Ip = returnAddress;
    }
}
