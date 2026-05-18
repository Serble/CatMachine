namespace CatVM.Testing;

/// <summary>
/// Direct tests for <see cref="InterruptHandlers"/>.
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
