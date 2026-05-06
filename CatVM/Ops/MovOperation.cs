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
        vm.ValidateMemoryRead(address, 4);
        uint value = BitConverter.ToUInt32(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
    }
    
    // Move from memory (immediate address) to register
    public static void MovRIP(CatVM vm) {
        byte destReg = vm.Read8();
        uint address = vm.ReadWord();
        vm.ValidateMemoryRead(address, 4);
        uint value = BitConverter.ToUInt32(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
    }

    public static void MovRPR(CatVM vm) {
        byte ptrReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        uint value = vm.Cpu.Get(srcReg);
        vm.ValidateMemoryWrite(address, 4);
        WriteU32(vm.Memory, address, value);
    }
    
    public static void MovRPI(CatVM vm) {
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        uint immediate = vm.ReadWord();
        vm.ValidateMemoryWrite(address, 4);
        WriteU32(vm.Memory, address, immediate);
    }
    
    public static void MovIPR(CatVM vm) {
        uint address = vm.ReadWord();
        byte srcReg = vm.Read8();
        uint value = vm.Cpu.Get(srcReg);
        vm.ValidateMemoryWrite(address, 4);
        WriteU32(vm.Memory, address, value);
    }
    
    public static void MovIPI(CatVM vm) {
        uint address = vm.ReadWord();
        uint immediate = vm.ReadWord();
        vm.ValidateMemoryWrite(address, 4);
        WriteU32(vm.Memory, address, immediate);
    }
    
    // Mov byte sized values
    
    public static void BMovIPR(CatVM vm) {
        uint address = vm.ReadWord();
        byte srcReg = vm.Read8();
        byte value = (byte)(vm.Cpu.Get(srcReg) & 0xFF);
        vm.ValidateMemoryWrite(address, 1);
        vm.Memory[address] = value;
    }
    
    public static void BMovRPR(CatVM vm) {
        byte ptrReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        byte value = (byte)(vm.Cpu.Get(srcReg) & 0xFF);
        vm.ValidateMemoryWrite(address, 1);
        vm.Memory[address] = value;
    }
    
    public static void BMovRIP(CatVM vm) {
        byte destReg = vm.Read8();
        uint address = vm.ReadWord();
        vm.ValidateMemoryRead(address, 1);
        byte value = vm.Memory[address];
        vm.Cpu.Set(destReg, value);
    }
    
    public static void BMovRRP(CatVM vm) {
        byte destReg = vm.Read8();
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        vm.ValidateMemoryRead(address, 1);
        byte value = vm.Memory[address];
        vm.Cpu.Set(destReg, value);
    }
    
    public static void BMovIPI(CatVM vm) {
        uint address = vm.ReadWord();
        byte immediate = vm.Read8();
        vm.ValidateMemoryWrite(address, 1);
        vm.Memory[address] = immediate;
    }
    
    public static void BMovRPI(CatVM vm) {
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        byte immediate = vm.Read8();
        vm.ValidateMemoryWrite(address, 1);
        vm.Memory[address] = immediate;
    }
    
    // Mov short sized values
    
    public static void SMovIPR(CatVM vm) {
        uint address = vm.ReadWord();
        byte srcReg = vm.Read8();
        ushort value = (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF);
        vm.ValidateMemoryWrite(address, 2);
        WriteU16(vm.Memory, address, value);
    }
    
    public static void SMovRPR(CatVM vm) {
        byte ptrReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        ushort value = (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF);
        vm.ValidateMemoryWrite(address, 2);
        WriteU16(vm.Memory, address, value);
    }
    
    public static void SMovRIP(CatVM vm) {
        byte destReg = vm.Read8();
        uint address = vm.ReadWord();
        vm.ValidateMemoryRead(address, 2);
        ushort value = BitConverter.ToUInt16(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
    }
    
    public static void SMovRRP(CatVM vm) {
        byte destReg = vm.Read8();
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        vm.ValidateMemoryRead(address, 2);
        ushort value = BitConverter.ToUInt16(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
    }
    
    public static void SMovIPI(CatVM vm) {
        uint address = vm.ReadWord();
        ushort immediate = vm.Read16();
        vm.ValidateMemoryWrite(address, 2);
        WriteU16(vm.Memory, address, immediate);
    }
    
    public static void SMovRPI(CatVM vm) {
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        ushort immediate = vm.Read16();
        vm.ValidateMemoryWrite(address, 2);
        WriteU16(vm.Memory, address, immediate);
    }
}
