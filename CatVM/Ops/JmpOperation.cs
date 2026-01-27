using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class JmpOperation {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void JmpRI(CatVM vm) => ConditionalJmp(vm, _ => true);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void JzRI(CatVM vm) => ConditionalJmp(vm, v => v.Cpu.ZeroFlag);  // jz
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void JnzRI(CatVM vm) => ConditionalJmp(vm, v => !v.Cpu.ZeroFlag);  // jnz
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void JlRI(CatVM vm) => ConditionalJmp(vm, v => v.Cpu.SignFlag != v.Cpu.OverflowFlag);  // jil
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void JleRI(CatVM vm) => ConditionalJmp(vm, v => v.Cpu.ZeroFlag || v.Cpu.SignFlag != v.Cpu.OverflowFlag);  // jile
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void JgRI(CatVM vm) => ConditionalJmp(vm, v => !v.Cpu.ZeroFlag && v.Cpu.SignFlag == v.Cpu.OverflowFlag);  // jug
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void JgeRI(CatVM vm) => ConditionalJmp(vm, v => v.Cpu.SignFlag == v.Cpu.OverflowFlag);  // jige
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void JaRI(CatVM vm) => ConditionalJmp(vm, v => !v.Cpu.CarryFlag && !v.Cpu.ZeroFlag);  // jug
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void JaeRI(CatVM vm) => ConditionalJmp(vm, v => !v.Cpu.CarryFlag);  // juge
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void JbRI(CatVM vm) => ConditionalJmp(vm, v => v.Cpu.CarryFlag);  // jul
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void JbeRI(CatVM vm) => ConditionalJmp(vm, v => v.Cpu.CarryFlag || v.Cpu.ZeroFlag);  // jule
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConditionalJmp(CatVM vm, Func<CatVM, bool> condition) {
        byte addressReg = vm.Read8();
        uint offset = vm.ReadWord();
        if (condition(vm)) {
            Jmp(vm, addressReg, offset);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Jmp(CatVM vm, byte addrReg, uint offset) {
        uint address = addrReg == 0xFF ? 0 : vm.Cpu.Get(addrReg);
        vm.Cpu.Ip = address + offset;
    }
}
