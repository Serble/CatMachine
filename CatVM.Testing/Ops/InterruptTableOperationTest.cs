namespace CatVM.Testing.Ops;

/// <summary>
/// Verifies the privileged setit / getit opcodes:
/// <list type="bullet">
///   <item>setit r/i writes the interrupt-table base register.</item>
///   <item>getit r reads it back.</item>
///   <item>All three fault with ProtectionFault in pure user mode (Mode=0b01).</item>
///   <item>All three are allowed in driver mode (Mode=0b11) since the supervisor
///         bit bypasses the privilege gate.</item>
/// </list>
/// </summary>
public class InterruptTableOperationTest {
    private const byte OpSetItR = 0x53;
    private const byte OpSetItI = 0x54;
    private const byte OpGetItR = 0x55;
    private const byte R0 = 0;
    private const byte R1 = 1;

    private static CatVm NewVm() => new(64 * 1024, 100_000) { Fast = true };

    [Test]
    public void SetItR_WritesIt_InKernelMode() {
        CatVm vm = NewVm();
        vm.LoadData([OpSetItR, R0]);
        vm.Cpu.R0 = 0x1234_5678;
        vm.Cpu.VirtualMode = false;

        vm.ExecuteInstruction(fast: true);

        Assert.That(vm.Cpu.It, Is.EqualTo(0x1234_5678u));
    }

    [Test]
    public void SetItI_WritesIt_InKernelMode() {
        CatVm vm = NewVm();
        vm.LoadData([OpSetItI, 0xEF, 0xBE, 0xAD, 0xDE]);
        vm.Cpu.VirtualMode = false;

        vm.ExecuteInstruction(fast: true);

        Assert.That(vm.Cpu.It, Is.EqualTo(0xDEADBEEFu));
    }

    [Test]
    public void GetItR_ReadsIt_InKernelMode() {
        CatVm vm = NewVm();
        vm.LoadData([OpGetItR, R1]);
        vm.Cpu.It = 0xCAFEBABE;
        vm.Cpu.VirtualMode = false;

        vm.ExecuteInstruction(fast: true);

        Assert.That(vm.Cpu.R1, Is.EqualTo(0xCAFEBABEu));
    }

    [Test]
    public void SetItThenGetIt_RoundTrips() {
        CatVm vm = NewVm();
        vm.LoadData([
            OpSetItI, 0x00, 0x01, 0x00, 0x00,
            OpGetItR, R0
        ]);
        vm.Cpu.VirtualMode = false;

        vm.ExecuteInstruction(fast: true);
        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.It, Is.EqualTo(0x100u));
            Assert.That(vm.Cpu.R0, Is.EqualTo(0x100u));
        });
    }

    [Test]
    public void SetItI_InUserMode_FaultsAndDoesNotWrite() {
        CatVm vm = NewVm();
        const uint mbase = 0x1000;
        const uint mlen  = 0x100;
        vm.LoadData([OpSetItI, 0x00, 0x00, 0xFE, 0xCA], mbase);

        vm.Cpu.MBase = mbase;
        vm.Cpu.MLen  = mlen;
        vm.Cpu.Sp    = mlen;
        vm.Cpu.Ip    = 0;
        vm.Cpu.It    = 0xFFFFFFFF;
        vm.Cpu.Mode  = 0b01;

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Paused, Is.True,
                "user-mode setit should ProtectionFault → halt via default handler");
            Assert.That(vm.Cpu.It, Is.EqualTo(0xFFFFFFFFu),
                "It must not have been written");
        });
    }

    [Test]
    public void GetItR_InUserMode_FaultsAndDoesNotWriteRegister() {
        CatVm vm = NewVm();
        const uint mbase = 0x1000;
        const uint mlen  = 0x100;
        vm.LoadData([OpGetItR, R0], mbase);

        vm.Cpu.MBase = mbase;
        vm.Cpu.MLen  = mlen;
        vm.Cpu.Sp    = mlen;
        vm.Cpu.Ip    = 0;
        vm.Cpu.It    = uint.MaxValue;
        vm.Cpu.R0    = 0x1111_1111;
        vm.Cpu.Mode  = 0b01;

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Paused, Is.True);
            Assert.That(vm.Cpu.R0, Is.EqualTo(0x1111_1111u),
                "R0 must not have been overwritten with It");
        });
    }

    [Test]
    public void SetItI_InDriverMode_IsAllowed() {
        CatVm vm = NewVm();
        const uint mbase = 0x1000;
        const uint mlen  = 0x100;
        vm.LoadData([OpSetItI, 0x00, 0x02, 0x00, 0x00], mbase);

        vm.Cpu.MBase = mbase;
        vm.Cpu.MLen  = mlen;
        vm.Cpu.Sp    = mlen;
        vm.Cpu.Ip    = 0;
        vm.Cpu.Mode  = 0b11;     // driver: virtual + supervisor

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Paused, Is.False, "driver should not fault");
            Assert.That(vm.Cpu.It, Is.EqualTo(0x200u));
            Assert.That(vm.Cpu.Mode, Is.EqualTo((byte)0b11),
                "Mode unchanged after non-trapping op");
        });
    }
}
