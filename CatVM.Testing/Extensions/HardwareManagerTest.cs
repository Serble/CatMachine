using CatVM.Extensions;
using CatVM.Serial;

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
}
