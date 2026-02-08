using CatVM.Serial;

namespace CatVM.Ops;

public static class SerialOperation {

    public static void InRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte portReg = vm.Read8();
        uint port = vm.Cpu.Get(portReg);
        vm.Cpu.Set(destReg, vm.GetSerialDevice(port).Input(vm));
    }
    
    public static void InRI(CatVM vm) {
        byte destReg = vm.Read8();
        uint port = vm.ReadWord();
        vm.Cpu.Set(destReg, vm.GetSerialDevice(port).Input(vm));
    }
    
    public static void OutRR(CatVM vm) {
        byte portReg = vm.Read8();
        byte dataReg = vm.Read8();
        uint port = vm.Cpu.Get(portReg);
        uint data = vm.Cpu.Get(dataReg);
        vm.GetSerialDevice(port).Output(vm, data);
    }
    
    public static void OutRI(CatVM vm) {
        byte portReg = vm.Read8();
        uint port = vm.Cpu.Get(portReg);
        uint data = vm.ReadWord();
        vm.GetSerialDevice(port).Output(vm, data);
    }
    
    public static void OutIR(CatVM vm) {
        uint port = vm.ReadWord();
        byte dataReg = vm.Read8();
        uint data = vm.Cpu.Get(dataReg);
        vm.GetSerialDevice(port).Output(vm, data);
    }
    
    public static void OutII(CatVM vm) {
        uint port = vm.ReadWord();
        uint data = vm.ReadWord();
        vm.GetSerialDevice(port).Output(vm, data);
    }
}
