namespace CatVM.Ops;

public static class SerialOperation {

    public static void InRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte portReg = vm.Read8(vm.Cpu.Ip + 2);
        uint port = vm.Cpu.Get(portReg);
        
        if (!vm.TryPrivileged()) {
            return;
        }

        ref uint dest = ref vm.Cpu.RegRef(destReg);
        vm.Cpu.Ip += 3;
        dest = vm.GetSerialDevice(port).Input(vm);
    }
    
    public static void InRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint port = vm.ReadWord(vm.Cpu.Ip + 2);
        
        if (!vm.TryPrivileged()) {
            return;
        }

        ref uint dest = ref vm.Cpu.RegRef(destReg);
        vm.Cpu.Ip += 6;
        dest = vm.GetSerialDevice(port).Input(vm);
    }
    
    public static void OutRR(CatVm vm) {
        byte portReg = vm.Read8(vm.Cpu.Ip + 1);
        byte dataReg = vm.Read8(vm.Cpu.Ip + 2);
        uint port = vm.Cpu.Get(portReg);
        uint data = vm.Cpu.Get(dataReg);
        
        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.Ip += 3;
        vm.GetSerialDevice(port).Output(vm, data);
    }
    
    public static void OutRI(CatVm vm) {
        byte portReg = vm.Read8(vm.Cpu.Ip + 1);
        uint port = vm.Cpu.Get(portReg);
        uint data = vm.ReadWord(vm.Cpu.Ip + 2);
        
        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.Ip += 6;
        vm.GetSerialDevice(port).Output(vm, data);
    }
    
    public static void OutIR(CatVm vm) {
        uint port = vm.ReadWord(vm.Cpu.Ip + 1);
        byte dataReg = vm.Read8(vm.Cpu.Ip + 5);
        uint data = vm.Cpu.Get(dataReg);
        
        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.Ip += 6;
        vm.GetSerialDevice(port).Output(vm, data);
    }
    
    public static void OutII(CatVm vm) {
        uint port = vm.ReadWord(vm.Cpu.Ip + 1);
        uint data = vm.ReadWord(vm.Cpu.Ip + 5);
        
        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.Ip += 9;
        vm.GetSerialDevice(port).Output(vm, data);
    }
}
