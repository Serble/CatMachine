using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class JmpOperation {

    public static void JmpRI(CatVM vm) => ConditionalJmp(vm, _ => true);
    public static void JzRI(CatVM vm) => ConditionalJmp(vm, v => v.Cpu.ZeroFlag);
    public static void JnzRI(CatVM vm) => ConditionalJmp(vm, v => !v.Cpu.ZeroFlag);
    public static void JilRI(CatVM vm) => ConditionalJmp(vm, v => v.Cpu.SignFlag != v.Cpu.OverflowFlag);
    public static void JileRI(CatVM vm) => ConditionalJmp(vm, v => v.Cpu.ZeroFlag || v.Cpu.SignFlag != v.Cpu.OverflowFlag);
    public static void JigRI(CatVM vm) => ConditionalJmp(vm, v => !v.Cpu.ZeroFlag && v.Cpu.SignFlag == v.Cpu.OverflowFlag);
    public static void JigeRI(CatVM vm) => ConditionalJmp(vm, v => v.Cpu.SignFlag == v.Cpu.OverflowFlag);
    public static void JugRI(CatVM vm) => ConditionalJmp(vm, v => !v.Cpu.CarryFlag && !v.Cpu.ZeroFlag);
    public static void JugeRI(CatVM vm) => ConditionalJmp(vm, v => !v.Cpu.CarryFlag);
    public static void JulRI(CatVM vm) => ConditionalJmp(vm, v => v.Cpu.CarryFlag);
    public static void JuleRI(CatVM vm) => ConditionalJmp(vm, v => v.Cpu.CarryFlag || v.Cpu.ZeroFlag);
    
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
