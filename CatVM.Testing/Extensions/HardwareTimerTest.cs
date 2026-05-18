using CatVM.Extensions;

namespace CatVM.Testing.Extensions;

/// <summary>
/// Verifies that <see cref="HardwareTimer"/> obeys the command protocol and
/// fires callbacks at the requested delay, surfacing the timer id via the
/// device's <c>Input</c> queue and raising the
/// <see cref="SpecialInterrupts.HardwareTimerCallback"/> interrupt.
/// </summary>
public class HardwareTimerTest {
    private const byte OpNop = 0x4D;

    /// <summary>
    /// Build a tiny interrupt vector table for HardwareTimerCallback (0x71).
    /// </summary>
    private static byte[] MakeIt(byte id, uint handlerAddr) {
        return [
            1,
            id,
            (byte)(handlerAddr & 0xFF),
            (byte)((handlerAddr >> 8) & 0xFF),
            (byte)((handlerAddr >> 16) & 0xFF),
            (byte)((handlerAddr >> 24) & 0xFF),
        ];
    }

    /// <summary>
    /// 1000 cycles/sec ⇒ each NOP advances virtual time by exactly 1ms,
    /// which makes the timer test deterministic without sleeping.
    /// Fast=false uses the virtual TicksPassed clock for event firing.
    /// </summary>
    private static CatVm NewVm(int memory = 512) {
        return new CatVm(memory, 1000) { Fast = false };
    }

    [Test]
    public void Probe_WriteZero_ReturnsType0x03() {
        CatVm vm = NewVm();
        HardwareTimer timer = new();
        timer.Output(vm, 0);
        Assert.That(timer.Input(vm), Is.EqualTo(0xB1F91A0C));
    }

    [Test]
    public void NewTimer_FiresAfterRequestedMilliseconds() {
        CatVm vm = NewVm();
        vm.LoadData(Enumerable.Repeat(OpNop, 16).ToArray());

        HardwareTimer timer = new();
        // NewTimer mode (1), 5ms delay, timer id = 0xCAFE
        timer.Output(vm, (uint)HardwareTimer.Mode.NewTimer);
        timer.Output(vm, 5);
        timer.Output(vm, 0xCAFE);

        // Timer should not fire before the 5ms (== 5 NOPs) elapses.
        for (int i = 0; i < 5; i++) {
            Assert.That(timer.Input(vm), Is.EqualTo(uint.MaxValue),
                $"Timer fired prematurely after {i} NOPs");
            vm.ExecuteInstruction();
        }

        // The 6th ExecuteInstruction sees CurrentPicosecondTime >= timer time
        // and fires the callback before executing the next NOP.
        vm.ExecuteInstruction();

        Assert.That(timer.Input(vm), Is.EqualTo((uint)0xCAFE),
            "Expected timer id to be enqueued for Input after firing");
    }

    [Test]
    public void NewTimer_FiringRaisesHardwareTimerCallbackInterrupt() {
        CatVm vm = NewVm();
        vm.LoadData(Enumerable.Repeat(OpNop, 16).ToArray());
        vm.LoadData([OpNop], 0x80);                // handler
        vm.LoadData(MakeIt(0x71, 0x80), 0x100);    // IT
        vm.Cpu.It = 0x100;
        vm.InterruptsEnabled = true;

        HardwareTimer timer = new();
        timer.Output(vm, (uint)HardwareTimer.Mode.NewTimer);
        timer.Output(vm, 1);   // 1ms == 1 NOP
        timer.Output(vm, 7);

        // Run enough ticks for the timer to fire and the handler NOP to execute.
        for (int i = 0; i < 5; i++) vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(timer.Input(vm), Is.EqualTo(7u),
                "Expected timer id 7 in input queue");
            Assert.That(vm.Cpu.Ip, Is.GreaterThanOrEqualTo(0x80u).And.LessThanOrEqualTo(0x90u),
                "Expected IP to be inside the interrupt handler region");
        });
    }

    [Test]
    public void MultipleTimers_FireInOrder() {
        CatVm vm = NewVm();
        vm.LoadData(Enumerable.Repeat(OpNop, 32).ToArray());

        HardwareTimer timer = new();
        // Schedule them out-of-delay-order to verify ordering by time, not insertion.
        timer.Output(vm, (uint)HardwareTimer.Mode.NewTimer);
        timer.Output(vm, 10);
        timer.Output(vm, 100);

        timer.Output(vm, (uint)HardwareTimer.Mode.NewTimer);
        timer.Output(vm, 3);
        timer.Output(vm, 200);

        timer.Output(vm, (uint)HardwareTimer.Mode.NewTimer);
        timer.Output(vm, 6);
        timer.Output(vm, 300);

        // Run more than 10 NOPs to allow them all to fire.
        for (int i = 0; i < 15; i++) vm.ExecuteInstruction();

        // Drain InputQueue and confirm time-ordering: 200 (3ms), 300 (6ms), 100 (10ms).
        List<uint> drained = [];
        for (int i = 0; i < 3; i++) drained.Add(timer.Input(vm));

        Assert.That(drained, Is.EqualTo(new uint[] { 200, 300, 100 }));
    }

    [Test]
    public void NewTimer_DoesNotFireBeforeDelay() {
        CatVm vm = NewVm();
        vm.LoadData(Enumerable.Repeat(OpNop, 8).ToArray());

        HardwareTimer timer = new();
        timer.Output(vm, (uint)HardwareTimer.Mode.NewTimer);
        timer.Output(vm, 100);  // 100ms — far beyond what we'll execute
        timer.Output(vm, 42);

        for (int i = 0; i < 5; i++) vm.ExecuteInstruction();

        Assert.That(timer.Input(vm), Is.EqualTo(uint.MaxValue),
            "Timer should not have fired yet");
    }

    [Test]
    public void Input_OnEmptyQueue_ReturnsUintMax() {
        CatVm vm = NewVm();
        HardwareTimer timer = new();
        Assert.That(timer.Input(vm), Is.EqualTo(uint.MaxValue));
    }
}
