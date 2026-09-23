using CatVM.Serial;
using HardwareManagerDevice;

namespace CatVM.Testing.Extensions;

public class HardwareManagerTest {
    private CatVm _vm = null!;

    [SetUp]
    public void Setup() {
        _vm = new CatVm(64, 10_000) { Fast = true };
    }

    [Test]
    public void Type_IsCorrect() {
        HardwareManager hm = new();
        Assert.That(hm.Type, Is.EqualTo(0x296C4EF5));
    }

    [Test]
    public void AutoDiscovery_ReturnsType() {
        HardwareManager hm = new();
        hm.Output(_vm, 0);
        Assert.That(hm.Input(_vm), Is.EqualTo(0x296C4EF5));
    }

    [Test]
    public void ListDevices_EnumeratesAllRegistered() {
        HardwareManager hm = new();
        _vm.RegisterSerialDevice(16, hm);
        _vm.RegisterSerialDevice(17, ISerialDevice.Create(0x99, _ => 0, (_, _) => {}));

        hm.Output(_vm, (uint)HardwareManager.Mode.ListDevices);

        // first read = count, then (port, type) pairs
        uint count = hm.Input(_vm);
        Assert.That(count, Is.EqualTo(2u));

        Dictionary<uint, uint> pairs = new();
        for (int i = 0; i < count; i++) {
            uint port = hm.Input(_vm);
            uint type = hm.Input(_vm);
            pairs[port] = type;
        }

        Assert.Multiple(() => {
            Assert.That(pairs[16], Is.EqualTo(0x296C4EF5));    // HardwareManager itself
            Assert.That(pairs[17], Is.EqualTo(0x99u));
        });
    }

    [Test]
    public void ResetSystem_ViaOutInstruction_LeavesIpAtZero() {
        _vm.RegisterSerialDevice(16, new HardwareManager());
        // OUT 16, ResetSystem — placed away from 0 so a clobbered IP (0x20 + 9) is distinguishable
        _vm.LoadData([0x4c, 16, 0x00, 0x00, 0x00, (byte)HardwareManager.Mode.ResetSystem, 0x00, 0x00, 0x00], 0x20);
        _vm.Cpu.Ip = 0x20;
        _vm.Cpu.R1 = 0xAAAA;

        _vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R1, Is.EqualTo(0u), "Reset should have run");
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(0u), "Instruction's IP advance must not clobber the reset");
        });
    }
}
