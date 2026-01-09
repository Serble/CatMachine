namespace CatVM.Ops;

public static class MovOperation {
    
    public static void MovRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint value = vm.Cpu.Get(srcReg);
        vm.Cpu.Set(destReg, value);
    }
    
    public static void MovRI(CatVM vm) {
        byte destReg = vm.Read8();
        uint immediate = vm.ReadWord();
        vm.Cpu.Set(destReg, immediate);
    }

    // Move from memory (pointer in register) to register
    public static void MovRRP(CatVM vm) {
        byte destReg = vm.Read8();
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        uint value = BitConverter.ToUInt32(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
    }
    
    // Move from memory (immediate address) to register
    public static void MovRIP(CatVM vm) {
        byte destReg = vm.Read8();
        uint address = vm.ReadWord();
        uint value = BitConverter.ToUInt32(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
    }
    
    public static void MovRPR(CatVM vm) {
        byte ptrReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        uint value = vm.Cpu.Get(srcReg);
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 4);
    }
    
    public static void MovRPI(CatVM vm) {
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        uint immediate = vm.ReadWord();
        byte[] bytes = BitConverter.GetBytes(immediate);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 4);
    }
    
    public static void MovIPR(CatVM vm) {
        uint address = vm.ReadWord();
        byte srcReg = vm.Read8();
        uint value = vm.Cpu.Get(srcReg);
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 4);
    }
    
    public static void MovIPI(CatVM vm) {
        uint address = vm.ReadWord();
        uint immediate = vm.ReadWord();
        byte[] bytes = BitConverter.GetBytes(immediate);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 4);
    }
    
    // Mov byte sized values
    
    public static void BMovIPR(CatVM vm) {
        uint address = vm.ReadWord();
        byte srcReg = vm.Read8();
        byte value = (byte)(vm.Cpu.Get(srcReg) & 0xFF);
        vm.Memory[address] = value;
    }
    
    public static void BMovRPR(CatVM vm) {
        byte ptrReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        byte value = (byte)(vm.Cpu.Get(srcReg) & 0xFF);
        vm.Memory[address] = value;
    }
    
    public static void BMovRIP(CatVM vm) {
        byte destReg = vm.Read8();
        uint address = vm.ReadWord();
        byte value = vm.Memory[address];
        vm.Cpu.Set(destReg, value);
    }
    
    public static void BMovRRP(CatVM vm) {
        byte destReg = vm.Read8();
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        byte value = vm.Memory[address];
        vm.Cpu.Set(destReg, value);
    }
    
    // Mov short sized values
    
    public static void SMovIPR(CatVM vm) {
        uint address = vm.ReadWord();
        byte srcReg = vm.Read8();
        ushort value = (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF);
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 2);
    }
    
    public static void SMovRPR(CatVM vm) {
        byte ptrReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        ushort value = (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF);
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 2);
    }
    
    public static void SMovRIP(CatVM vm) {
        byte destReg = vm.Read8();
        uint address = vm.ReadWord();
        ushort value = BitConverter.ToUInt16(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
    }
    
    public static void SMovRRP(CatVM vm) {
        byte destReg = vm.Read8();
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        ushort value = BitConverter.ToUInt16(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
    }
}
