namespace CatVM.Ops;

/// <summary>
/// Privileged opcodes for accessing the kernel-stack-base register Ksp.
/// <para/>
/// Ksp is a fixed kernel-stack-base value that hardware reloads into Sp on
/// every user→kernel interrupt entry; the kernel writes it once per
/// process scheduling. It is never written by iret.
/// </summary>
public static class KspOperation {

    public static void SetKspR(CatVm vm) {
        byte reg = vm.Read8();

        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.Ksp = vm.Cpu.Get(reg);
    }

    public static void SetKspI(CatVm vm) {
        uint imm = vm.ReadWord();

        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.Ksp = imm;
    }

    public static void GetKspR(CatVm vm) {
        byte reg = vm.Read8();

        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Cpu.Set(reg, vm.Cpu.Ksp);
    }
}
