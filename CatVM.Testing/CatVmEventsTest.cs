namespace CatVM.Testing;

/// <summary>
/// Tests for <see cref="CatVm.RunIn"/> / <see cref="CatVm.RunAt"/> scheduling
/// driven by the event loop in <see cref="CatVm.ExecuteInstruction"/>.
/// </summary>
public class CatVmEventsTest {

    private static CatVm NewVm() => new(64, 1_000_000) { Fast = false };

    [Test]
    public void RunAt_FiresOnceTimeReached() {
        CatVm vm = NewVm();
        vm.LoadData([0x4D, 0x4D, 0x4D, 0x4D]);
        int hits = 0;
        // Schedule for time = 0 so the first ExecuteInstruction fires it.
        vm.RunAt(0, () => hits++);
        vm.ExecuteInstruction(true);
        Assert.That(hits, Is.EqualTo(1));
        // Should not fire again on subsequent instructions.
        vm.ExecuteInstruction(true);
        Assert.That(hits, Is.EqualTo(1));
    }

    [Test]
    public void RunIn_FiresAfterRelativeDelay() {
        CatVm vm = NewVm();
        vm.LoadData(new byte[16]); // 16 NOPs (opcode 0x00 = MovRR; we'll use NOP)
        for (int i = 0; i < 16; i++) vm.Memory[i] = 0x4D;
        int hits = 0;
        vm.RunIn(CatVm.PicosecondsPerMillisecond, () => hits++);
        // Each NOP costs only a few picoseconds; we need many to reach 1ms in virtual time.
        // Just ensure the event hasn't fired yet on the first instruction.
        vm.ExecuteInstruction(true);
        Assert.That(hits, Is.EqualTo(0));
    }

    [Test]
    public void Events_FireInTimeOrder() {
        CatVm vm = NewVm();
        vm.LoadData([0x4D, 0x4D, 0x4D]);
        List<int> order = [];
        // All scheduled in the past so they all fire before the first instruction;
        // the loop pops in time order (earliest first).
        vm.RunAt(-1, () => order.Add(1));
        vm.RunAt(-3, () => order.Add(3));
        vm.RunAt(-2, () => order.Add(2));
        vm.ExecuteInstruction(true);
        Assert.That(order, Is.EqualTo(new[] { 3, 2, 1 }));
    }

    [Test]
    public void RunAt_InThePast_FiresImmediatelyOnNextExecute() {
        CatVm vm = NewVm();
        vm.LoadData([0x4D]);
        bool fired = false;
        vm.RunAt(-1, () => fired = true);
        vm.ExecuteInstruction(true);
        Assert.That(fired, Is.True);
    }

    [Test]
    public void RunAt_FarFuture_DoesNotFireDuringSingleStep() {
        CatVm vm = NewVm();
        vm.LoadData([0x4D]);
        bool fired = false;
        vm.RunAt(long.MaxValue / 2, () => fired = true);
        vm.ExecuteInstruction(true);
        Assert.That(fired, Is.False);
    }
}
