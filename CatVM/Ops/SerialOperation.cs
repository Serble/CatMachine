namespace CatVM.Ops;

public static class SerialOperation {

    public static void InRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte portReg = vm.Read8(vm.Cpu.Ip + 2);
        uint port = vm.Cpu.Get(portReg);
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.Cpu.Set(destReg, vm.GetSerialDevice(port).Input(vm));
        vm.Cpu.Ip += 3;
    }
    
    public static void InRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint port = vm.ReadWord(vm.Cpu.Ip + 2);
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.Cpu.Set(destReg, vm.GetSerialDevice(port).Input(vm));
        vm.Cpu.Ip += 6;
    }
    
    public static void OutRR(CatVm vm) {
        byte portReg = vm.Read8(vm.Cpu.Ip + 1);
        byte dataReg = vm.Read8(vm.Cpu.Ip + 2);
        uint port = vm.Cpu.Get(portReg);
        uint data = vm.Cpu.Get(dataReg);
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.GetSerialDevice(port).Output(vm, data);
        vm.Cpu.Ip += 3;
    }
    
    public static void OutRI(CatVm vm) {
        byte portReg = vm.Read8(vm.Cpu.Ip + 1);
        uint port = vm.Cpu.Get(portReg);
        uint data = vm.ReadWord(vm.Cpu.Ip + 2);
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.GetSerialDevice(port).Output(vm, data);
        vm.Cpu.Ip += 6;
    }
    
    public static void OutIR(CatVm vm) {
        uint port = vm.ReadWord(vm.Cpu.Ip + 1);
        byte dataReg = vm.Read8(vm.Cpu.Ip + 5);
        uint data = vm.Cpu.Get(dataReg);
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.GetSerialDevice(port).Output(vm, data);
        vm.Cpu.Ip += 6;
    }
    
    public static void OutII(CatVm vm) {
        uint port = vm.ReadWord(vm.Cpu.Ip + 1);
        uint data = vm.ReadWord(vm.Cpu.Ip + 5);
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.GetSerialDevice(port).Output(vm, data);
        vm.Cpu.Ip += 9;
    }
}
