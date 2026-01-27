using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class StackOperation {
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PushR(CatVM vm) {
        byte srcReg = vm.Read8();
        uint value = vm.Cpu.Get(srcReg);
        vm.StackPush(value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PushI(CatVM vm) {
        uint immediate = vm.ReadWord();
        vm.StackPush(immediate);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Push8R(CatVM vm) {
        byte srcReg = vm.Read8();
        byte value = (byte)(vm.Cpu.Get(srcReg) & 0xFF);
        vm.StackPush(value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Push8I(CatVM vm) {
        byte immediate = vm.Read8();
        vm.StackPush(immediate);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Push16R(CatVM vm) {
        byte srcReg = vm.Read8();
        ushort value = (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF);
        vm.StackPush(value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Push16I(CatVM vm) {
        ushort immediate = vm.Read16();
        vm.StackPush(immediate);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PopR(CatVM vm) {
        byte destReg = vm.Read8();
        uint value = vm.StackPop();
        vm.Cpu.Set(destReg, value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pop8R(CatVM vm) {
        byte destReg = vm.Read8();
        byte value = vm.StackPop8();
        vm.Cpu.Set(destReg, value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Pop16R(CatVM vm) {
        byte destReg = vm.Read8();
        ushort value = vm.StackPop16();
        vm.Cpu.Set(destReg, value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Call(CatVM vm) {
        byte addressReg = vm.Read8();
        uint offset = vm.ReadWord();
        vm.StackPush(vm.Cpu.Ip);
        JmpOperation.Jmp(vm, addressReg, offset);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Ret(CatVM vm) {
        uint returnAddress = vm.StackPop();
        vm.Cpu.Ip = returnAddress;
    }
}
