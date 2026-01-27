using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class MovOperation {
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MovRR(CatVM vm) {
        byte destReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint value = vm.Cpu.Get(srcReg);
        vm.Cpu.Set(destReg, value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MovRI(CatVM vm) {
        byte destReg = vm.Read8();
        uint immediate = vm.ReadWord();
        vm.Cpu.Set(destReg, immediate);
    }

    // Move from memory (pointer in register) to register
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MovRRP(CatVM vm) {
        byte destReg = vm.Read8();
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        vm.ValidateMemoryRead(address, 4);
        uint value = BitConverter.ToUInt32(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
    }
    
    // Move from memory (immediate address) to register
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MovRIP(CatVM vm) {
        byte destReg = vm.Read8();
        uint address = vm.ReadWord();
        vm.ValidateMemoryRead(address, 4);
        uint value = BitConverter.ToUInt32(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MovRPR(CatVM vm) {
        byte ptrReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        uint value = vm.Cpu.Get(srcReg);
        byte[] bytes = BitConverter.GetBytes(value);
        vm.ValidateMemoryWrite(address, 4);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 4);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MovRPI(CatVM vm) {
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        uint immediate = vm.ReadWord();
        byte[] bytes = BitConverter.GetBytes(immediate);
        vm.ValidateMemoryWrite(address, 4);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 4);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MovIPR(CatVM vm) {
        uint address = vm.ReadWord();
        byte srcReg = vm.Read8();
        uint value = vm.Cpu.Get(srcReg);
        byte[] bytes = BitConverter.GetBytes(value);
        vm.ValidateMemoryWrite(address, 4);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 4);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MovIPI(CatVM vm) {
        uint address = vm.ReadWord();
        uint immediate = vm.ReadWord();
        byte[] bytes = BitConverter.GetBytes(immediate);
        vm.ValidateMemoryWrite(address, 4);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 4);
    }
    
    // Mov byte sized values
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BMovIPR(CatVM vm) {
        uint address = vm.ReadWord();
        byte srcReg = vm.Read8();
        byte value = (byte)(vm.Cpu.Get(srcReg) & 0xFF);
        vm.ValidateMemoryWrite(address, 1);
        vm.Memory[address] = value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BMovRPR(CatVM vm) {
        byte ptrReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        byte value = (byte)(vm.Cpu.Get(srcReg) & 0xFF);
        vm.ValidateMemoryWrite(address, 1);
        vm.Memory[address] = value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BMovRIP(CatVM vm) {
        byte destReg = vm.Read8();
        uint address = vm.ReadWord();
        vm.ValidateMemoryRead(address, 1);
        byte value = vm.Memory[address];
        vm.Cpu.Set(destReg, value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BMovRRP(CatVM vm) {
        byte destReg = vm.Read8();
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        vm.ValidateMemoryRead(address, 1);
        byte value = vm.Memory[address];
        vm.Cpu.Set(destReg, value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BMovIPI(CatVM vm) {
        uint address = vm.ReadWord();
        byte immediate = vm.Read8();
        vm.ValidateMemoryWrite(address, 1);
        vm.Memory[address] = immediate;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void BMovRPI(CatVM vm) {
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        byte immediate = vm.Read8();
        vm.ValidateMemoryWrite(address, 1);
        vm.Memory[address] = immediate;
    }
    
    // Mov short sized values
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SMovIPR(CatVM vm) {
        uint address = vm.ReadWord();
        byte srcReg = vm.Read8();
        ushort value = (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF);
        byte[] bytes = BitConverter.GetBytes(value);
        vm.ValidateMemoryWrite(address, 2);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 2);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SMovRPR(CatVM vm) {
        byte ptrReg = vm.Read8();
        byte srcReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        ushort value = (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF);
        byte[] bytes = BitConverter.GetBytes(value);
        vm.ValidateMemoryWrite(address, 2);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 2);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SMovRIP(CatVM vm) {
        byte destReg = vm.Read8();
        uint address = vm.ReadWord();
        vm.ValidateMemoryRead(address, 2);
        ushort value = BitConverter.ToUInt16(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SMovRRP(CatVM vm) {
        byte destReg = vm.Read8();
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        vm.ValidateMemoryRead(address, 2);
        ushort value = BitConverter.ToUInt16(vm.Memory, (int)address);
        vm.Cpu.Set(destReg, value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SMovIPI(CatVM vm) {
        uint address = vm.ReadWord();
        ushort immediate = vm.Read16();
        byte[] bytes = BitConverter.GetBytes(immediate);
        vm.ValidateMemoryWrite(address, 2);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 2);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SMovRPI(CatVM vm) {
        byte ptrReg = vm.Read8();
        uint address = vm.Cpu.Get(ptrReg);
        ushort immediate = vm.Read16();
        byte[] bytes = BitConverter.GetBytes(immediate);
        vm.ValidateMemoryWrite(address, 2);
        Array.Copy(bytes, 0, vm.Memory, (int)address, 2);
    }
}
