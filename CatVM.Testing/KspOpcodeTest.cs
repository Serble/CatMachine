namespace CatVM.Testing;

/// <summary>
/// Verifies the privileged setksp / getksp opcodes:
/// <list type="bullet">
///   <item>setksp r/i writes the kernel-stack-base register.</item>
///   <item>getksp r reads it back.</item>
///   <item>All three fault with ProtectionFault in pure user mode (Mode=0b01)
///         and leave Ksp / the destination register and Ip unchanged.</item>
///   <item>All three are allowed in driver mode (Mode=0b11) since the supervisor
///         bit bypasses the privilege gate.</item>
/// </list>
/// </summary>
public class KspOpcodeTest {
    private const byte OpSetKspR = 0x56;
    private const byte OpSetKspI = 0x57;
    private const byte OpGetKspR = 0x58;
    private const byte R0 = 0;
    private const byte R1 = 1;

    private static CatVm NewVm() => new(64 * 1024, 100_000) { Fast = true };

    [Test]
    public void SetKspR_WritesKsp_InKernelMode() {
        CatVm vm = NewVm();
        vm.LoadData([OpSetKspR, R0]);
        vm.Cpu.R0 = 0x1234_5678;
        vm.Cpu.VirtualMode = false;

        vm.ExecuteInstruction(fast: true);

        Assert.That(vm.Cpu.Ksp, Is.EqualTo(0x1234_5678u));
        Assert.That(vm.Cpu.Ip, Is.EqualTo(2u));
    }

    [Test]
    public void SetKspI_WritesKsp_InKernelMode() {
        CatVm vm = NewVm();
        vm.LoadData([OpSetKspI, 0xEF, 0xBE, 0xAD, 0xDE]);   // little-endian 0xDEADBEEF
        vm.Cpu.VirtualMode = false;

        vm.ExecuteInstruction(fast: true);

        Assert.That(vm.Cpu.Ksp, Is.EqualTo(0xDEADBEEFu));
        Assert.That(vm.Cpu.Ip, Is.EqualTo(5u));
    }

    [Test]
    public void GetKspR_ReadsKsp_InKernelMode() {
        CatVm vm = NewVm();
        vm.LoadData([OpGetKspR, R1]);
        vm.Cpu.Ksp = 0xCAFEBABE;
        vm.Cpu.VirtualMode = false;

        vm.ExecuteInstruction(fast: true);

        Assert.That(vm.Cpu.R1, Is.EqualTo(0xCAFEBABEu));
        Assert.That(vm.Cpu.Ip, Is.EqualTo(2u));
    }

    [Test]
    public void SetKspThenGetKsp_RoundTrips() {
        CatVm vm = NewVm();
        vm.LoadData([
            OpSetKspI, 0x00, 0xF0, 0x00, 0x00,    // setksp 0xF000
            OpGetKspR, R0                          // R0 := Ksp
        ]);
        vm.Cpu.VirtualMode = false;

        vm.ExecuteInstruction(fast: true);
        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Ksp, Is.EqualTo(0xF000u));
            Assert.That(vm.Cpu.R0, Is.EqualTo(0xF000u));
        });
    }

    // ---- Privilege gates ----

    private static void SetupUserMode(CatVm vm) {
        const uint mbase = 0x1000;
        const uint mlen  = 0x100;
        vm.Cpu.MBase = mbase;
        vm.Cpu.MLen  = mlen;
        vm.Cpu.Sp    = mlen;
        vm.Cpu.Ip    = 0;
        vm.Cpu.It    = uint.MaxValue;
        vm.Cpu.Mode  = 0b01;   // pure user
    }

    [Test]
    public void SetKspR_InUserMode_FaultsAndDoesNotWrite() {
        CatVm vm = NewVm();
        vm.LoadData([OpSetKspR, R0], 0x1000);
        vm.Cpu.R0  = 0xDEAD_BEEF;
        vm.Cpu.Ksp = 0x1111_1111;
        SetupUserMode(vm);

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Paused, Is.True, "user-mode setksp should ProtectionFault → halt");
            Assert.That(vm.Cpu.Ksp, Is.EqualTo(0x1111_1111u), "Ksp must not have been written");
            Assert.That(vm.Cpu.Ip, Is.EqualTo(0u), "faulting op must not advance Ip");
        });
    }

    [Test]
    public void SetKspI_InUserMode_FaultsAndDoesNotWrite() {
        CatVm vm = NewVm();
        vm.LoadData([OpSetKspI, 0xEF, 0xBE, 0xAD, 0xDE], 0x1000);
        vm.Cpu.Ksp = 0x1111_1111;
        SetupUserMode(vm);

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Paused, Is.True);
            Assert.That(vm.Cpu.Ksp, Is.EqualTo(0x1111_1111u), "Ksp must not have been written");
            Assert.That(vm.Cpu.Ip, Is.EqualTo(0u), "faulting op must not advance Ip");
        });
    }

    [Test]
    public void GetKspR_InUserMode_FaultsAndDoesNotWriteRegister() {
        CatVm vm = NewVm();
        vm.LoadData([OpGetKspR, R0], 0x1000);
        vm.Cpu.Ksp = 0xCAFEBABE;
        vm.Cpu.R0  = 0x2222_2222;
        SetupUserMode(vm);

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Paused, Is.True);
            Assert.That(vm.Cpu.R0, Is.EqualTo(0x2222_2222u), "R0 must not have been overwritten with Ksp");
            Assert.That(vm.Cpu.Ip, Is.EqualTo(0u), "faulting op must not advance Ip");
        });
    }

    [Test]
    public void SetKspR_InDriverMode_IsAllowed() {
        CatVm vm = NewVm();
        vm.LoadData([OpSetKspR, R0], 0x1000);
        vm.Cpu.R0 = 0x9000;
        SetupUserMode(vm);
        vm.Cpu.Mode = 0b11;   // driver: virtual + supervisor

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Paused, Is.False, "driver should not fault");
            Assert.That(vm.Cpu.Ksp, Is.EqualTo(0x9000u));
            Assert.That(vm.Cpu.Ip, Is.EqualTo(2u));
        });
    }
}
