namespace CatVM.Testing;

/// <summary>
/// Direct tests for <see cref="InterruptHandlers"/>. <c>ShutdownInterrupt</c> is
/// intentionally not covered as it calls <see cref="Environment.Exit"/>.
/// </summary>
public class InterruptHandlersTest {
    private CatVm _vm = null!;
    private TextWriter _origOut = null!;
    private StringWriter _captured = null!;

    [SetUp]
    public void Setup() {
        _vm = new CatVm(1024, 10_000) { Fast = true };
        _origOut = Console.Out;
        _captured = new StringWriter();
        Console.SetOut(_captured);
    }

    [TearDown]
    public void TearDown() {
        Console.SetOut(_origOut);
        _captured.Dispose();
    }

    [Test]
    public void HaltInterrupt_SetsPaused() {
        Assert.That(_vm.Paused, Is.False);
        InterruptHandlers.HaltInterrupt(_vm);
        Assert.That(_vm.Paused, Is.True);
    }

    [Test]
    public void ResetInterrupt_RestoresInitialState() {
        _vm.Cpu.R0 = 0xAAAAAAAA;
        _vm.Cpu.Ip = 0x100;
        _vm.Memory[0] = 0xFF;

        InterruptHandlers.ResetInterrupt(_vm);

        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R0, Is.EqualTo(0u));
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(0u));
            Assert.That(_vm.Memory[0], Is.EqualTo((byte)0));
        });
    }

    [Test]
    public void PrintNumInterrupt_PrintsR1DecimalAndHex() {
        _vm.Cpu.R1 = 0x1F;
        InterruptHandlers.PrintNumInterrupt(_vm);
        string output = _captured.ToString();
        Assert.Multiple(() => {
            Assert.That(output, Does.Contain("31"));
            Assert.That(output, Does.Contain("0x0000001f"));
        });
    }

    [Test]
    public void DefaultHandler_HaltsForCpuFault() {
        InterruptHandlers.DefaultHandler(_vm, 0x03); // ProtectionFault < 0x10
        Assert.That(_vm.Paused, Is.True);
    }

    [Test]
    public void DefaultHandler_IgnoresNonFaultInterrupt() {
        InterruptHandlers.DefaultHandler(_vm, 0x42);
        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.False);
            Assert.That(_captured.ToString(), Is.Empty);
        });
    }
}
