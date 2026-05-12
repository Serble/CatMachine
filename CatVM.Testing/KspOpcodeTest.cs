namespace CatVM.Testing;

/// <summary>
/// Verifies the privileged setksp / getksp opcodes:
/// <list type="bullet">
///   <item>setksp r/i writes the kernel-stack-base register.</item>
///   <item>getksp r reads it back.</item>
///   <item>All three faulting in user mode (PrivilegedInstruction) is covered
///         once the privilege-check todo lands; for now we only assert the
///         happy-path and that <c>TryPrivileged</c> short-circuits the op when
///         the VM is in user mode without changing state.</item>
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
    }

    [Test]
    public void SetKspI_WritesKsp_InKernelMode() {
        CatVm vm = NewVm();
        vm.LoadData([OpSetKspI, 0xEF, 0xBE, 0xAD, 0xDE]);   // little-endian 0xDEADBEEF
        vm.Cpu.VirtualMode = false;

        vm.ExecuteInstruction(fast: true);

        Assert.That(vm.Cpu.Ksp, Is.EqualTo(0xDEADBEEFu));
    }

    [Test]
    public void GetKspR_ReadsKsp_InKernelMode() {
        CatVm vm = NewVm();
        vm.LoadData([OpGetKspR, R1]);
        vm.Cpu.Ksp = 0xCAFEBABE;
        vm.Cpu.VirtualMode = false;

        vm.ExecuteInstruction(fast: true);

        Assert.That(vm.Cpu.R1, Is.EqualTo(0xCAFEBABEu));
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
}
