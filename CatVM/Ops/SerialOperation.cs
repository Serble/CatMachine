namespace CatVM.Ops;

public static class SerialOperation {

    public static void InRR(CatVm vm) {
        byte destReg = vm.Read8();
        byte portReg = vm.Read8();
        uint port = vm.Cpu.Get(portReg);
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.Cpu.Set(destReg, vm.GetSerialDevice(port).Input(vm));
    }
    
    public static void InRI(CatVm vm) {
        byte destReg = vm.Read8();
        uint port = vm.ReadWord();
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.Cpu.Set(destReg, vm.GetSerialDevice(port).Input(vm));
    }
    
    public static void OutRR(CatVm vm) {
        byte portReg = vm.Read8();
        byte dataReg = vm.Read8();
        uint port = vm.Cpu.Get(portReg);
        uint data = vm.Cpu.Get(dataReg);
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.GetSerialDevice(port).Output(vm, data);
    }
    
    public static void OutRI(CatVm vm) {
        byte portReg = vm.Read8();
        uint port = vm.Cpu.Get(portReg);
        uint data = vm.ReadWord();
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.GetSerialDevice(port).Output(vm, data);
    }
    
    public static void OutIR(CatVm vm) {
        uint port = vm.ReadWord();
        byte dataReg = vm.Read8();
        uint data = vm.Cpu.Get(dataReg);
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.GetSerialDevice(port).Output(vm, data);
    }
    
    public static void OutII(CatVm vm) {
        uint port = vm.ReadWord();
        uint data = vm.ReadWord();
        
        if (!vm.TryPrivileged()) {
            return;
        }
        
        vm.GetSerialDevice(port).Output(vm, data);
    }
}
