namespace CatVM.Ops;

using System.Runtime.CompilerServices;

public static class MovOperation {

    /// <summary>
    /// Allocation free uint store.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteU32(byte[] memory, uint address, uint value) {
        // Bounds check happens implicitly via the array indexer in WriteUnaligned. We do an
        // explicit check on the high byte first to make the failure mode (IndexOutOfRangeException)
        // identical to the previous Array.Copy path.
        _ = memory[address + 3];
        Unsafe.WriteUnaligned(ref memory[address], value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteU16(byte[] memory, uint address, ushort value) {
        _ = memory[address + 1];
        Unsafe.WriteUnaligned(ref memory[address], value);
    }
    
    public static void MovRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        uint value = vm.Cpu.Get(srcReg);
        vm.Cpu.Set(destReg, value);
        vm.Cpu.Ip += 3;
    }
    
    public static void MovRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        vm.Cpu.Set(destReg, immediate);
        vm.Cpu.Ip += 6;
    }

    // Move from memory (pointer in register) to register
    public static void MovRRP(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 2);
        uint address = vm.Cpu.Get(ptrReg);
        vm.ValidateMemoryRead(address, 4);
        uint value = BitConverter.ToUInt32(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
        vm.Cpu.Ip += 3;
    }
    
    // Move from memory (immediate address) to register
    public static void MovRIP(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint address = vm.ReadWord(vm.Cpu.Ip + 2);
        vm.ValidateMemoryRead(address, 4);
        uint value = BitConverter.ToUInt32(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
        vm.Cpu.Ip += 6;
    }

    public static void MovRPR(CatVm vm) {
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        uint address = vm.Cpu.Get(ptrReg);
        uint value = vm.Cpu.Get(srcReg);
        vm.ValidateMemoryWrite(address, 4);
        WriteU32(vm.Memory, address, value);
        vm.Cpu.Ip += 3;
    }
    
    public static void MovRPI(CatVm vm) {
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 1);
        uint address = vm.Cpu.Get(ptrReg);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        vm.ValidateMemoryWrite(address, 4);
        WriteU32(vm.Memory, address, immediate);
        vm.Cpu.Ip += 6;
    }
    
    public static void MovIPR(CatVm vm) {
        uint address = vm.ReadWord(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 5);
        uint value = vm.Cpu.Get(srcReg);
        vm.ValidateMemoryWrite(address, 4);
        WriteU32(vm.Memory, address, value);
        vm.Cpu.Ip += 6;
    }
    
    public static void MovIPI(CatVm vm) {
        uint address = vm.ReadWord(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 5);
        vm.ValidateMemoryWrite(address, 4);
        WriteU32(vm.Memory, address, immediate);
        vm.Cpu.Ip += 9;
    }
    
    // Mov byte sized values
    
    public static void BMovIPR(CatVm vm) {
        uint address = vm.ReadWord(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 5);
        byte value = (byte)(vm.Cpu.Get(srcReg) & 0xFF);
        vm.ValidateMemoryWrite(address, 1);
        vm.Memory[address] = value;
        vm.Cpu.Ip += 6;
    }
    
    public static void BMovRPR(CatVm vm) {
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        uint address = vm.Cpu.Get(ptrReg);
        byte value = (byte)(vm.Cpu.Get(srcReg) & 0xFF);
        vm.ValidateMemoryWrite(address, 1);
        vm.Memory[address] = value;
        vm.Cpu.Ip += 3;
    }
    
    public static void BMovRIP(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint address = vm.ReadWord(vm.Cpu.Ip + 2);
        vm.ValidateMemoryRead(address, 1);
        byte value = vm.Memory[address];
        vm.Cpu.Set(destReg, value);
        vm.Cpu.Ip += 6;
    }
    
    public static void BMovRRP(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 2);
        uint address = vm.Cpu.Get(ptrReg);
        vm.ValidateMemoryRead(address, 1);
        byte value = vm.Memory[address];
        vm.Cpu.Set(destReg, value);
        vm.Cpu.Ip += 3;
    }
    
    public static void BMovIPI(CatVm vm) {
        uint address = vm.ReadWord(vm.Cpu.Ip + 1);
        byte immediate = vm.Read8(vm.Cpu.Ip + 5);
        vm.ValidateMemoryWrite(address, 1);
        vm.Memory[address] = immediate;
        vm.Cpu.Ip += 6;
    }
    
    public static void BMovRPI(CatVm vm) {
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 1);
        uint address = vm.Cpu.Get(ptrReg);
        byte immediate = vm.Read8(vm.Cpu.Ip + 2);
        vm.ValidateMemoryWrite(address, 1);
        vm.Memory[address] = immediate;
        vm.Cpu.Ip += 3;
    }
    
    // Mov short sized values
    
    public static void SMovIPR(CatVm vm) {
        uint address = vm.ReadWord(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 5);
        ushort value = (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF);
        vm.ValidateMemoryWrite(address, 2);
        WriteU16(vm.Memory, address, value);
        vm.Cpu.Ip += 6;
    }
    
    public static void SMovRPR(CatVm vm) {
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        uint address = vm.Cpu.Get(ptrReg);
        ushort value = (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF);
        vm.ValidateMemoryWrite(address, 2);
        WriteU16(vm.Memory, address, value);
        vm.Cpu.Ip += 3;
    }
    
    public static void SMovRIP(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint address = vm.ReadWord(vm.Cpu.Ip + 2);
        vm.ValidateMemoryRead(address, 2);
        ushort value = BitConverter.ToUInt16(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
        vm.Cpu.Ip += 6;
    }
    
    public static void SMovRRP(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 2);
        uint address = vm.Cpu.Get(ptrReg);
        vm.ValidateMemoryRead(address, 2);
        ushort value = BitConverter.ToUInt16(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
        vm.Cpu.Ip += 3;
    }
    
    public static void SMovIPI(CatVm vm) {
        uint address = vm.ReadWord(vm.Cpu.Ip + 1);
        ushort immediate = vm.Read16(vm.Cpu.Ip + 5);
        vm.ValidateMemoryWrite(address, 2);
        WriteU16(vm.Memory, address, immediate);
        vm.Cpu.Ip += 7;
    }
    
    public static void SMovRPI(CatVm vm) {
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 1);
        uint address = vm.Cpu.Get(ptrReg);
        ushort immediate = vm.Read16(vm.Cpu.Ip + 2);
        vm.ValidateMemoryWrite(address, 2);
        WriteU16(vm.Memory, address, immediate);
        vm.Cpu.Ip += 4;
    }
}
