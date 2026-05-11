using CatVM.Serial;

namespace CatVM.Testing;

/// <summary>
/// Tests for <see cref="CatVM"/>'s public surface that aren't covered by
/// the per-opcode tests: lifecycle (Reset/LoadData), serial-device registry,
/// physical vs translated reads, exception mapping in
/// <see cref="CatVM.ExecuteWithErrorHandling"/>, and system-interrupt dispatch.
/// </summary>
public class CatVmCoreTest {

    private static CatVM NewVm(int mem = 256, byte[]? rom = null) =>
        new(mem, 10_000, rom) { Fast = true };

    // ---- Reset -----------------------------------------------------------

    [Test]
    public void Reset_ClearsCpuAndMemory() {
        CatVM vm = NewVm();
        vm.Cpu.R0 = 0xAB;
        vm.Memory[0] = 0xFF;
        vm.Reset();
        Assert.Multiple(() => {
            Assert.That(vm.Cpu.R0, Is.EqualTo(0u));
            Assert.That(vm.Memory[0], Is.EqualTo((byte)0));
            Assert.That(vm.Cpu.Sp, Is.EqualTo(256u));
            Assert.That(vm.Cpu.MLen, Is.EqualTo(256u));
        });
    }

    [Test]
    public void Reset_PreserveMem_KeepsMemoryButResetsCpu() {
        CatVM vm = NewVm();
        vm.Memory[5] = 0xDE;
        vm.Cpu.R0 = 0x42;
        vm.Reset(preserveMem: true);
        Assert.Multiple(() => {
            Assert.That(vm.Cpu.R0, Is.EqualTo(0u));
            Assert.That(vm.Memory[5], Is.EqualTo((byte)0xDE));
        });
    }

    [Test]
    public void Reset_ReloadsRom() {
        CatVM vm = NewVm(rom: [0xAA, 0xBB]);
        vm.Memory[0] = 0xFF;
        vm.Reset();
        Assert.That(vm.Memory[0], Is.EqualTo((byte)0xAA));
        Assert.That(vm.Memory[1], Is.EqualTo((byte)0xBB));
    }

    // ---- Constructor -----------------------------------------------------

    [Test]
    public void Ctor_RomLargerThanMemory_Throws() {
        Assert.Throws<Exception>(() => new CatVM(2, 10_000, [1, 2, 3, 4]));
    }

    // ---- LoadData --------------------------------------------------------

    [Test]
    public void LoadData_BeyondMemory_Throws() {
        CatVM vm = NewVm(mem: 16);
        Assert.Throws<Exception>(() => vm.LoadData(new byte[8], address: 12));
    }

    [Test]
    public void LoadData_ExactFit_Succeeds() {
        CatVM vm = NewVm(mem: 16);
        vm.LoadData([1, 2, 3, 4, 5, 6, 7, 8], 8);
        Assert.That(vm.Memory[15], Is.EqualTo((byte)8));
    }

    // ---- Serial device registry -----------------------------------------

    [Test]
    public void RegisterSerialDevice_DuplicatePort_Throws() {
        CatVM vm = NewVm();
        vm.RegisterSerialDevice(20, ISerialDevice.Null);
        Assert.Throws<Exception>(() => vm.RegisterSerialDevice(20, ISerialDevice.Null));
    }

    [Test]
    public void RegisterSerialDevice_AutoPort_PicksFirstFree() {
        CatVM vm = NewVm();
        vm.RegisterSerialDevice(0, ISerialDevice.Null);
        vm.RegisterSerialDevice(2, ISerialDevice.Null);
        vm.RegisterSerialDevice(ISerialDevice.Null);  // should pick 1
        Assert.That(vm.SerialDevices.ContainsKey(1), Is.True);
    }

    [Test]
    public void GetSerialDevice_Unregistered_ReturnsNull() {
        CatVM vm = NewVm();
        ISerialDevice d = vm.GetSerialDevice(0xFE);
        Assert.That(d.Type, Is.EqualTo(uint.MaxValue));
    }

    // ---- Physical reads bypass translation -------------------------------

    [Test]
    public void Read8Physical_BypassesVirtualModeTranslation() {
        CatVM vm = NewVm(mem: 256);
        vm.Memory[0x10] = 0xAB;
        vm.Cpu.MBase = 0x80;
        vm.Cpu.MLen = 0x10;
        vm.Cpu.Mode = 0b01;  // virtual mode
        // Translated read of virtual 0x10 would be OOB; physical bypasses.
        Assert.That(vm.Read8Physical(0x10), Is.EqualTo((byte)0xAB));
    }

    [Test]
    public void ReadWordPhysical_Bypass() {
        CatVM vm = NewVm(mem: 256);
        BitConverter.GetBytes(0xCAFEBABEu).CopyTo(vm.Memory, 0x20);
        vm.Cpu.MBase = 0x80;
        vm.Cpu.MLen = 0x10;
        vm.Cpu.Mode = 0b01;
        Assert.That(vm.ReadWordPhysical(0x20), Is.EqualTo(0xCAFEBABEu));
    }

    // ---- ExecuteWithErrorHandling exception mapping ---------------------

    [Test]
    public void ErrorHandling_DivideByZero_HaltsViaDefaultHandler() {
        CatVM vm = NewVm();
        vm.ExecuteWithErrorHandling(() => throw new DivideByZeroException());
        Assert.That(vm.Paused, Is.True);
    }

    [Test]
    public void ErrorHandling_MemoryOutOfRange_RaisesPageFault() {
        CatVM vm = NewVm();
        vm.ExecuteWithErrorHandling(() => throw new MemoryOutOfRange(false, 0, 1));
        Assert.That(vm.Paused, Is.True);
    }

    [Test]
    public void ErrorHandling_GenericException_RaisesInvalidInstruction() {
        CatVM vm = NewVm();
        vm.ExecuteWithErrorHandling(() => throw new InvalidOperationException("boom"));
        Assert.That(vm.Paused, Is.True);
    }

    [Test]
    public void ErrorHandling_ArgumentException_RaisesPageFault() {
        CatVM vm = NewVm();
        vm.ExecuteWithErrorHandling(() => throw new ArgumentException("bad arg"));
        Assert.That(vm.Paused, Is.True);
    }

    [Test]
    public void ErrorHandling_DumpErrors_WritesToConsoleWhenEnabled() {
        CatVM vm = new(64, 10_000) { Fast = true, DumpErrors = true };
        TextWriter old = Console.Out;
        StringWriter w = new();
        Console.SetOut(w);
        try {
            vm.ExecuteWithErrorHandling(() => throw new InvalidOperationException("dump-me"));
        }
        finally {
            Console.SetOut(old);
        }
        Assert.That(w.ToString(), Does.Contain("dump-me"));
    }

    // ---- HandleInterrupt system codes -----------------------------------

    [Test]
    public void HandleInterrupt_0x81_HaltsViaSystemHandler() {
        CatVM vm = NewVm();
        vm.HandleInterrupt(0x81);
        Assert.That(vm.Paused, Is.True);
    }

    [Test]
    public void HandleInterrupt_0x83_TriggersReset() {
        CatVM vm = NewVm();
        vm.Memory[0] = 0xFF;
        vm.Cpu.R0 = 0xAA;
        vm.HandleInterrupt(0x83);
        Assert.Multiple(() => {
            Assert.That(vm.Cpu.R0, Is.EqualTo(0u));
            Assert.That(vm.Memory[0], Is.EqualTo((byte)0));
        });
    }

    [Test]
    public void HandleInterrupt_0x90_GatedByEnableTestingInterrupts() {
        // Capture stdout
        TextWriter old = Console.Out;
        StringWriter w = new();
        Console.SetOut(w);
        try {
            CatVM disabled = NewVm();
            disabled.Cpu.R1 = 0x42;
            disabled.HandleInterrupt(0x90);
            string disabledOut = w.ToString();

            CatVM enabled = new(256, 10_000) { Fast = true, EnableTestingInterrupts = true };
            enabled.Cpu.R1 = 0x42;
            w.GetStringBuilder().Clear();
            enabled.HandleInterrupt(0x90);
            string enabledOut = w.ToString();

            Assert.Multiple(() => {
                Assert.That(disabledOut, Does.Not.Contain("66"),
                    "0x90 must be ignored when EnableTestingInterrupts=false");
                Assert.That(enabledOut, Does.Contain("66"));
            });
        }
        finally {
            Console.SetOut(old);
        }
    }

    [Test]
    public void HandleInterrupt_NoIt_UnknownCode_NoOp() {
        CatVM vm = NewVm();
        // 0x42 with It=MaxValue and opcode>=0x10 -> DefaultHandler returns immediately.
        vm.HandleInterrupt(0x42);
        Assert.That(vm.Paused, Is.False);
    }

    // ---- Hardware interrupt queue ---------------------------------------

    [Test]
    public void HardwareInterrupt_QueuedAndDeliveredOnNextExecute() {
        CatVM vm = NewVm();
        vm.LoadData([0x4D, 0x4D]); // two NOPs
        vm.HardwareInterrupt(0x81); // halt code
        vm.ExecuteInstruction(true);
        Assert.That(vm.Paused, Is.True);
    }

    [Test]
    public void HardwareInterrupt_NotDelivered_WhenInterruptsDisabled() {
        CatVM vm = NewVm();
        vm.InterruptsEnabled = false;
        vm.LoadData([0x4D]);
        vm.HardwareInterrupt(0x81);
        vm.ExecuteInstruction(true);
        Assert.That(vm.Paused, Is.False, "disabled queue should not fire halt");
    }

    // ---- CyclesPerSecond round-trip -------------------------------------

    [Test]
    public void CyclesPerSecond_RoundTripsThroughPicosPerCycle() {
        CatVM vm = NewVm();
        vm.CyclesPerSecond = 50_000;
        Assert.That(vm.CyclesPerSecond, Is.EqualTo(50_000u));
    }

    // ---- Paused getter/setter side effect on Runtime --------------------

    [Test]
    public void Paused_StopsAndResumesRuntimeStopwatch() {
        CatVM vm = NewVm();
        vm.Paused = true;
        Assert.That(vm.Runtime.IsRunning, Is.False);
        vm.Paused = false;
        Assert.That(vm.Runtime.IsRunning, Is.True);
    }
}
