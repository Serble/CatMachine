using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class JmpOperation {

    public static void JmpRI(CatVm vm) => ConditionalJmp(vm, _ => true);
    public static void JzRI(CatVm vm) => ConditionalJmp(vm, v => v.Cpu.ZeroFlag);
    public static void JnzRI(CatVm vm) => ConditionalJmp(vm, v => !v.Cpu.ZeroFlag);
    public static void JilRI(CatVm vm) => ConditionalJmp(vm, v => v.Cpu.SignFlag != v.Cpu.OverflowFlag);
    public static void JileRI(CatVm vm) => ConditionalJmp(vm, v => v.Cpu.ZeroFlag || v.Cpu.SignFlag != v.Cpu.OverflowFlag);
    public static void JigRI(CatVm vm) => ConditionalJmp(vm, v => !v.Cpu.ZeroFlag && v.Cpu.SignFlag == v.Cpu.OverflowFlag);
    public static void JigeRI(CatVm vm) => ConditionalJmp(vm, v => v.Cpu.SignFlag == v.Cpu.OverflowFlag);
    public static void JugRI(CatVm vm) => ConditionalJmp(vm, v => !v.Cpu.CarryFlag && !v.Cpu.ZeroFlag);
    public static void JugeRI(CatVm vm) => ConditionalJmp(vm, v => !v.Cpu.CarryFlag);
    public static void JulRI(CatVm vm) => ConditionalJmp(vm, v => v.Cpu.CarryFlag);
    public static void JuleRI(CatVm vm) => ConditionalJmp(vm, v => v.Cpu.CarryFlag || v.Cpu.ZeroFlag);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ConditionalJmp(CatVm vm, Func<CatVm, bool> condition) {
        byte addressReg = vm.Read8(vm.Cpu.Ip + 1);
        uint offset = vm.ReadWord(vm.Cpu.Ip + 2);
        if (condition(vm)) {
            Jmp(vm, addressReg, offset);
        }
        else {
            vm.Cpu.Ip += 6;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Jmp(CatVm vm, byte addrReg, uint offset) {
        uint address = addrReg == 0xFF ? 0 : vm.Cpu.Get(addrReg);
        vm.Cpu.Ip = address + offset;
    }
}
