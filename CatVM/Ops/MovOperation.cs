namespace CatVM.Ops;

public static class MovOperation {

    public static void MovRR(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        uint value = vm.Cpu.Get(srcReg);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        vm.Cpu.Ip += 3;
        dest = value;
    }

    public static void MovRI(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        vm.Cpu.Ip += 6;
        dest = immediate;
    }

    // Move from memory (pointer in register) to register
    public static void MovRRP(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 2);
        uint address = vm.Cpu.Get(ptrReg);
        uint value = vm.ReadWord(address);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        vm.Cpu.Ip += 3;
        dest = value;
    }

    // Move from memory (immediate address) to register
    public static void MovRIP(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint address = vm.ReadWord(vm.Cpu.Ip + 2);
        uint value = vm.ReadWord(address);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        vm.Cpu.Ip += 6;
        dest = value;
    }

    public static void MovRPR(CatVm vm) {
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        uint address = vm.Cpu.Get(ptrReg);
        vm.WriteWord(address, vm.Cpu.Get(srcReg));
        vm.Cpu.Ip += 3;
    }

    public static void MovRPI(CatVm vm) {
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 1);
        uint address = vm.Cpu.Get(ptrReg);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 2);
        vm.WriteWord(address, immediate);
        vm.Cpu.Ip += 6;
    }

    public static void MovIPR(CatVm vm) {
        uint address = vm.ReadWord(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 5);
        vm.WriteWord(address, vm.Cpu.Get(srcReg));
        vm.Cpu.Ip += 6;
    }

    public static void MovIPI(CatVm vm) {
        uint address = vm.ReadWord(vm.Cpu.Ip + 1);
        uint immediate = vm.ReadWord(vm.Cpu.Ip + 5);
        vm.WriteWord(address, immediate);
        vm.Cpu.Ip += 9;
    }

    // Mov byte sized values

    public static void BMovIPR(CatVm vm) {
        uint address = vm.ReadWord(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 5);
        vm.Write8(address, (byte)(vm.Cpu.Get(srcReg) & 0xFF));
        vm.Cpu.Ip += 6;
    }

    public static void BMovRPR(CatVm vm) {
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        uint address = vm.Cpu.Get(ptrReg);
        vm.Write8(address, (byte)(vm.Cpu.Get(srcReg) & 0xFF));
        vm.Cpu.Ip += 3;
    }

    public static void BMovRIP(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint address = vm.ReadWord(vm.Cpu.Ip + 2);
        uint value = vm.Read8(address);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        vm.Cpu.Ip += 6;
        dest = value;
    }

    public static void BMovRRP(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 2);
        uint address = vm.Cpu.Get(ptrReg);
        uint value = vm.Read8(address);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        vm.Cpu.Ip += 3;
        dest = value;
    }

    public static void BMovIPI(CatVm vm) {
        uint address = vm.ReadWord(vm.Cpu.Ip + 1);
        byte immediate = vm.Read8(vm.Cpu.Ip + 5);
        vm.Write8(address, immediate);
        vm.Cpu.Ip += 6;
    }

    public static void BMovRPI(CatVm vm) {
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 1);
        uint address = vm.Cpu.Get(ptrReg);
        byte immediate = vm.Read8(vm.Cpu.Ip + 2);
        vm.Write8(address, immediate);
        vm.Cpu.Ip += 3;
    }

    // Mov short sized values

    public static void SMovIPR(CatVm vm) {
        uint address = vm.ReadWord(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 5);
        vm.Write16(address, (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF));
        vm.Cpu.Ip += 6;
    }

    public static void SMovRPR(CatVm vm) {
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 1);
        byte srcReg = vm.Read8(vm.Cpu.Ip + 2);
        uint address = vm.Cpu.Get(ptrReg);
        vm.Write16(address, (ushort)(vm.Cpu.Get(srcReg) & 0xFFFF));
        vm.Cpu.Ip += 3;
    }

    public static void SMovRIP(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        uint address = vm.ReadWord(vm.Cpu.Ip + 2);
        uint value = vm.Read16(address);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        vm.Cpu.Ip += 6;
        dest = value;
    }

    public static void SMovRRP(CatVm vm) {
        byte destReg = vm.Read8(vm.Cpu.Ip + 1);
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 2);
        uint address = vm.Cpu.Get(ptrReg);
        uint value = vm.Read16(address);
        ref uint dest = ref vm.Cpu.RegRef(destReg);
        vm.Cpu.Ip += 3;
        dest = value;
    }

    public static void SMovIPI(CatVm vm) {
        uint address = vm.ReadWord(vm.Cpu.Ip + 1);
        ushort immediate = vm.Read16(vm.Cpu.Ip + 5);
        vm.Write16(address, immediate);
        vm.Cpu.Ip += 7;
    }

    public static void SMovRPI(CatVm vm) {
        byte ptrReg = vm.Read8(vm.Cpu.Ip + 1);
        uint address = vm.Cpu.Get(ptrReg);
        ushort immediate = vm.Read16(vm.Cpu.Ip + 2);
        vm.Write16(address, immediate);
        vm.Cpu.Ip += 4;
    }
}
