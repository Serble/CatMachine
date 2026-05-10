namespace CatVM.Testing;

/// <summary>
/// Verifies that hardware interrupts are dispatched correctly: queued, gated by
/// <see cref="CatVM.InterruptsEnabled"/>, routed through the IT vector table when
/// installed, fall through to the default handler otherwise, and are safe to enqueue
/// from arbitrary threads.
/// </summary>
public class HardwareInterruptTest {
    private const byte OpNop = 0x4D;

    /// <summary>
    /// Build a tiny interrupt vector table: one (id, handler) pair followed by zeros.
    /// Layout matches CatVM.HandleInterrupt: u8 entryCount, then [u8 id, u32 handler]*.
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

    private static CatVM NewVm(int memory = 512, uint cyclesPerSecond = 10_000) {
        // Fast = true so ExecuteInstruction never sleeps for "real" timing.
        return new CatVM(memory, cyclesPerSecond) { Fast = true };
    }

    [Test]
    public void HardwareInterrupt_DispatchedOnNextExecute_WhenEnabled() {
        CatVM vm = NewVm();
        // Single NOP at address 0; interrupt handler at address 0x40 also a NOP.
        vm.LoadData([OpNop]);
        vm.LoadData([OpNop], 0x40);
        vm.LoadData(MakeIt(0x71, 0x40), 0x100);
        vm.Cpu.It = 0x100;
        vm.Cpu.Ip = 0;
        vm.InterruptsEnabled = true;

        uint spBefore = vm.Cpu.Sp;
        vm.HardwareInterrupt(SpecialInterrupts.HardwareTimerCallback); // 0x71

        vm.ExecuteInstruction(fast: true);

        // The interrupt handler should have been entered: IP was pushed and IP set to 0x40.
        // Then the NOP at 0x40 ran, advancing IP to 0x41.
        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Ip, Is.EqualTo(0x41u),
                "Expected IP to be at handler+1 after dispatch + NOP execution");
            Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore - 5),
                "Expected return address + marker to have been pushed (Sp decremented by 5)");
        });
    }

    [Test]
    public void HardwareInterrupt_NotDispatched_WhenDisabled() {
        CatVM vm = NewVm();
        vm.LoadData([OpNop]);
        vm.LoadData([OpNop], 0x40);
        vm.LoadData(MakeIt(0x71, 0x40), 0x100);
        vm.Cpu.It = 0x100;
        vm.Cpu.Ip = 0;
        vm.InterruptsEnabled = false;

        uint spBefore = vm.Cpu.Sp;
        vm.HardwareInterrupt(SpecialInterrupts.HardwareTimerCallback);

        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Ip, Is.EqualTo(1u),
                "Expected IP to advance past the in-place NOP, not jump to a handler");
            Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore),
                "Expected nothing to have been pushed onto the stack");
        });
    }

    [Test]
    public void HardwareInterrupt_QueuedWhileDisabled_DispatchedAfterEnable() {
        CatVM vm = NewVm();
        vm.LoadData([OpNop, OpNop]);
        vm.LoadData([OpNop], 0x40);
        vm.LoadData(MakeIt(0x71, 0x40), 0x100);
        vm.Cpu.It = 0x100;
        vm.Cpu.Ip = 0;
        vm.InterruptsEnabled = false;

        vm.HardwareInterrupt(SpecialInterrupts.HardwareTimerCallback);

        // Run while disabled: interrupt sits in the queue.
        vm.ExecuteInstruction(fast: true);
        Assert.That(vm.Cpu.Ip, Is.EqualTo(1u), "Disabled tick should not have dispatched");

        // Enable and run again: the queued interrupt should now fire.
        vm.InterruptsEnabled = true;
        uint spBefore = vm.Cpu.Sp;
        vm.ExecuteInstruction(fast: true);

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Ip, Is.EqualTo(0x41u),
                "Expected pending interrupt to dispatch on first enabled tick");
            Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore - 5),
                "Expected return address to have been pushed");
        });
    }

    [Test]
    public void HardwareInterrupt_OnePerExecuteInstruction() {
        CatVM vm = NewVm();
        // Handler at 0x40 just NOPs; we don't return from it, we only care that two
        // queued interrupts take two ExecuteInstruction calls to drain.
        vm.LoadData([OpNop, OpNop, OpNop]);
        vm.LoadData([OpNop], 0x40);
        vm.LoadData(MakeIt(0x71, 0x40), 0x100);
        vm.Cpu.It = 0x100;
        vm.Cpu.Ip = 0;
        vm.InterruptsEnabled = true;

        vm.HardwareInterrupt(SpecialInterrupts.HardwareTimerCallback);
        vm.HardwareInterrupt(SpecialInterrupts.HardwareTimerCallback);

        uint spBefore = vm.Cpu.Sp;

        // Tick 1: dispatch first interrupt (push IP=0, jump to 0x40), then run NOP at 0x40.
        vm.ExecuteInstruction(fast: true);
        Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore - 5),
            "First tick should push exactly one return address");
        Assert.That(vm.Cpu.Ip, Is.EqualTo(0x41u),
            "First tick should land in the handler and execute its NOP");

        // Tick 2: dispatch second interrupt (push IP=0x41, jump to 0x40), then run NOP.
        vm.ExecuteInstruction(fast: true);
        Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore - 10),
            "Second tick should push a second return address");
        Assert.That(vm.Cpu.Ip, Is.EqualTo(0x41u),
            "Second tick should re-enter the handler");
    }

    [Test]
    public void HardwareInterrupt_FallsThroughToDefaultHandler_WhenIdNotInTable() {
        CatVM vm = NewVm();
        vm.LoadData([OpNop]);
        // Vector table only has an entry for 0x71; we'll fire 0x73.
        vm.LoadData(MakeIt(0x71, 0x40), 0x100);
        vm.Cpu.It = 0x100;
        vm.Cpu.Ip = 0;
        vm.InterruptsEnabled = true;

        uint spBefore = vm.Cpu.Sp;
        vm.HardwareInterrupt(SpecialInterrupts.NicNotification); // 0x73

        // Should not throw, should not push (default handler is a no-op for unknown IDs),
        // and the in-place NOP should still execute.
        Assert.DoesNotThrow(() => vm.ExecuteInstruction(fast: true));
        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore),
                "Default handler should not push a return address");
            Assert.That(vm.Cpu.Ip, Is.EqualTo(1u),
                "Inline NOP should still have run");
        });
    }

    [Test]
    public void HardwareInterrupt_NoVectorTable_UsesDefaultHandler() {
        CatVM vm = NewVm();
        vm.LoadData([OpNop]);
        // Cpu.It defaults to uint.MaxValue meaning "no table".
        vm.Cpu.Ip = 0;
        vm.InterruptsEnabled = true;

        uint spBefore = vm.Cpu.Sp;
        vm.HardwareInterrupt(SpecialInterrupts.HardwareTimerCallback);

        Assert.DoesNotThrow(() => vm.ExecuteInstruction(fast: true));
        Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore),
            "With no IT installed the default handler should not touch the stack");
    }

    [Test]
    public void HardwareInterrupt_EnqueueIsThreadSafe() {
        // Stress: many threads racing to enqueue should not throw or lose interrupts
        // (the underlying ConcurrentQueue<byte> guarantees atomic enqueue).
        CatVM vm = NewVm();
        vm.LoadData([OpNop]);
        // Handler at 0x40 just NOPs; we won't drain so we can count enqueues by Sp pushes.
        vm.LoadData([OpNop], 0x40);
        vm.LoadData(MakeIt(0x71, 0x40), 0x100);
        vm.Cpu.It = 0x100;
        vm.InterruptsEnabled = false; // hold them all in the queue first

        const int threads = 8;
        const int perThread = 1000;
        const int total = threads * perThread;

        Task[] tasks = new Task[threads];
        for (int t = 0; t < threads; t++) {
            tasks[t] = Task.Run(() => {
                for (int i = 0; i < perThread; i++) {
                    vm.HardwareInterrupt(SpecialInterrupts.HardwareTimerCallback);
                }
            });
        }
        Task.WaitAll(tasks);

        // Now drain. Each ExecuteInstruction dispatches at most one queued interrupt,
        // pushes the return address + marker (Sp -= 5), then executes the handler's NOP.
        // We need `total` ticks to drain everything. Memory is 512 bytes; each push uses
        // 5 bytes. Use a vm with enough stack room.
        CatVM bigVm = new(64 * 1024, 10_000) { Fast = true };
        bigVm.LoadData([OpNop]);
        bigVm.LoadData([OpNop], 0x40);
        bigVm.LoadData(MakeIt(0x71, 0x40), 0x100);
        bigVm.Cpu.It = 0x100;
        bigVm.InterruptsEnabled = false;

        Task[] tasks2 = new Task[threads];
        for (int t = 0; t < threads; t++) {
            tasks2[t] = Task.Run(() => {
                for (int i = 0; i < perThread; i++) {
                    bigVm.HardwareInterrupt(SpecialInterrupts.HardwareTimerCallback);
                }
            });
        }
        Task.WaitAll(tasks2);

        bigVm.InterruptsEnabled = true;
        uint spBefore = bigVm.Cpu.Sp;

        for (int i = 0; i < total; i++) {
            bigVm.ExecuteInstruction(fast: true);
        }

        uint pushed = (spBefore - bigVm.Cpu.Sp) / 5;
        Assert.That(pushed, Is.EqualTo((uint)total),
            $"Expected exactly {total} interrupts to dispatch (one push of 5 bytes each), got {pushed}");
    }

    [Test]
    public void HardwareInterrupt_DoesNotDispatchWhenQueueEmpty() {
        CatVM vm = NewVm();
        vm.LoadData([OpNop, OpNop, OpNop]);
        vm.LoadData([OpNop], 0x40);
        vm.LoadData(MakeIt(0x71, 0x40), 0x100);
        vm.Cpu.It = 0x100;
        vm.InterruptsEnabled = true;

        uint spBefore = vm.Cpu.Sp;
        for (int i = 0; i < 3; i++) {
            vm.ExecuteInstruction(fast: true);
        }

        Assert.Multiple(() => {
            Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore),
                "No interrupts queued -> no pushes should occur");
            Assert.That(vm.Cpu.Ip, Is.EqualTo(3u),
                "Three NOPs should advance IP by 3");
        });
    }

    [Test]
    public void Interrupt_SoftwareDispatchesImmediately_RegardlessOfEnabled() {
        // Software Interrupt() bypasses the queue and InterruptsEnabled. Verify both states.
        foreach (bool enabled in new[] { true, false }) {
            CatVM vm = NewVm();
            vm.LoadData([OpNop]);
            vm.LoadData([OpNop], 0x40);
            vm.LoadData(MakeIt(0x71, 0x40), 0x100);
            vm.Cpu.It = 0x100;
            vm.Cpu.Ip = 0;
            vm.InterruptsEnabled = enabled;

            uint spBefore = vm.Cpu.Sp;
            vm.Interrupt(SpecialInterrupts.HardwareTimerCallback);

            Assert.Multiple(() => {
                Assert.That(vm.Cpu.Ip, Is.EqualTo(0x40u),
                    $"Software Interrupt should jump to handler immediately (enabled={enabled})");
                Assert.That(vm.Cpu.Sp, Is.EqualTo(spBefore - 5),
                    $"Software Interrupt should push the return address (enabled={enabled})");
            });
        }
    }
}
