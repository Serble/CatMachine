namespace CatVM.Ops;

public static class StackOperation {
    
    public static void PushR(CatVm vm) {
        byte srcReg = vm.Read8(vm.Cpu.Ip + 1);
        uint value = vm.Cpu.Get(srcReg);
        vm.StackPush(value);
        vm.Cpu.Ip += 2;
    }
    
    public static void PushI(CatVm vm) {
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 1);
        vm.StackPush(immediate);
        vm.Cpu.Ip += 5;
    }
    
    public static void Push8R(CatVm vm) {
        byte srcReg = vm.Read8(vm.Cpu.Ip + 1);
        byte value = (byte)(vm.Cpu.Get(srcReg) & 0xFF);
        vm.StackPush(value);
        vm.Cpu.Ip += 2;
    }
    
    public static void Push8I(CatVm vm) {
        byte immediate = vm.Read8(vm.Cpu.Ip + 1);
        vm.StackPush(immediate);
        vm.Cpu.Ip += 2;
    }
    
    public static void Push16R(CatVm vm) {
        byte srcReg = vm.Read8(vm.Cpu.Ip + 1);
        ushort value = (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF);
        vm.StackPush(value);
        vm.Cpu.Ip += 2;
    }
    
    public static void Push16I(CatVm vm) {
        ushort immediate = vm.Read16(vm.Cpu.Ip + 1);
        vm.StackPush(immediate);
        vm.Cpu.Ip += 3;
    }
    
    public static void PopR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        uint value = vm.StackPop();
        vm.Cpu.Ip += 2;
        dest = value;
    }
    
    public static void Pop8R(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        byte value = vm.StackPop8();
        vm.Cpu.Ip += 2;
        dest = value;
    }
    
    public static void Pop16R(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        ushort value = vm.StackPop16();
        vm.Cpu.Ip += 2;
        dest = value;
    }
    
    public static void Call(CatVm vm) {
        byte addressReg = vm.Read8(vm.Cpu.Ip + 1);
        uint offset = vm.ReadWord(vm.Cpu.Ip + 2);
        // resolve the target before pushing so a bad register faults without touching the stack
        uint target = JmpOperation.Target(vm, addressReg, offset);
        vm.StackPush(vm.Cpu.Ip + 6);
        vm.Cpu.Ip = target;
    }
    
    public static void Ret(CatVm vm) {
        uint returnAddress = vm.StackPop();
        vm.Cpu.Ip = returnAddress;
    }
}
