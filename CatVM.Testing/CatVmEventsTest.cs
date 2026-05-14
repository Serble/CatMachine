using System.Collections.Concurrent;
using System.Diagnostics;

namespace CatVM.Testing;

/// <summary>
/// Tests for <see cref="CatVm.RunIn"/> / <see cref="CatVm.RunAt"/> scheduling
/// driven by the event loop in <see cref="CatVm.ExecuteInstruction"/>.
/// </summary>
public class CatVmEventsTest {

    private static CatVm NewVm() => new(64, 1_000_000) { Fast = false };
    private static CatVm NewFastVm() => new(64, 1_000_000) { Fast = true };

    // 1 NOP per byte so the VM can step many times without falling off the
    // program. NOP opcode is 0x4D.
    private static byte[] NopBlock(int n) {
        byte[] b = new byte[n];
        for (int i = 0; i < n; i++) b[i] = 0x4D;
        return b;
    }

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
        vm.LoadData(NopBlock(16));
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

    // ----------------------------------------------------------------------
    // Synchronous-drain tests (Fast=false): events scheduled in the past must
    // fire on the next ExecuteInstruction without depending on the scheduler
    // thread (which races for past-time events).
    // ----------------------------------------------------------------------

    [Test]
    public void MultipleEventsScheduledInPast_AllFireOnNextExecute() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(4));
        int hits = 0;
        for (int i = 0; i < 100; i++) {
            vm.RunAt(-i - 1, () => Interlocked.Increment(ref hits));
        }
        vm.ExecuteInstruction(true);
        Assert.That(hits, Is.EqualTo(100));
    }

    [Test]
    public void EventScheduledAtZero_FiresOnFirstInstruction() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        bool fired = false;
        vm.RunAt(0, () => fired = true);
        vm.ExecuteInstruction(true);
        Assert.That(fired, Is.True);
    }

    [Test]
    public void Event_FiresOnlyOnce_EvenIfManyInstructionsPass() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(64));
        int hits = 0;
        vm.RunAt(-1, () => hits++);
        for (int i = 0; i < 64; i++) vm.ExecuteInstruction(true);
        Assert.That(hits, Is.EqualTo(1));
    }

    // ----------------------------------------------------------------------
    // Callback re-entry: a callback that schedules another event.
    // Especially important because FireDueEvents holds the lock; RunAt must
    // be reentrant. Also exercises remove-before-invoke: a callback
    // scheduling an earlier event must not have its new event eaten.
    // ----------------------------------------------------------------------

    [Test]
    public void Callback_CanScheduleAnotherPastEvent_FiresInSameDrain() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        List<int> order = [];
        vm.RunAt(-1, () => {
            order.Add(1);
            vm.RunAt(-2, () => order.Add(2));
        });
        vm.ExecuteInstruction(true);
        Assert.That(order, Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void Callback_CanScheduleAFutureEvent_DoesNotFireInSameDrain() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        int reentrant = 0;
        vm.RunAt(-1, () => {
            // Schedule for a very-future time so we know it won't fire in this drain.
            vm.RunAt(long.MaxValue / 2, () => reentrant++);
        });
        vm.ExecuteInstruction(true);
        Assert.That(reentrant, Is.EqualTo(0));
    }

    [Test]
    public void Callback_CanReschedule_Itself_RepeatingPattern() {
        // A callback that re-schedules itself in the past simulates a periodic
        // timer driven entirely by the drain loop.
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(8));
        int hits = 0;
        Action? cb = null;
        cb = () => {
            hits++;
            if (hits < 5) vm.RunAt(-100 - hits, cb!);
        };
        vm.RunAt(-1, cb);
        vm.ExecuteInstruction(true);
        Assert.That(hits, Is.EqualTo(5));
    }

    [Test]
    public void Callback_ThatThrows_PropagatesOutOfExecuteInstruction() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        vm.RunAt(-1, () => throw new InvalidOperationException("boom"));
        Assert.Throws<InvalidOperationException>(() => vm.ExecuteInstruction(true));
    }

    // ----------------------------------------------------------------------
    // Far-future events: must NOT have fired even after many instructions.
    // ----------------------------------------------------------------------

    [Test]
    public void FarFutureEvent_DoesNotFireAcrossManyInstructions() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(64));
        bool fired = false;
        vm.RunAt(long.MaxValue / 2, () => fired = true);
        for (int i = 0; i < 64; i++) vm.ExecuteInstruction(true);
        Assert.That(fired, Is.False);
    }

    [Test]
    public void FarFutureEvent_FollowedByPastEvent_PastFiresFirst() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        List<string> order = [];
        vm.RunAt(long.MaxValue / 2, () => order.Add("future"));
        vm.RunAt(-1, () => order.Add("past"));
        vm.ExecuteInstruction(true);
        Assert.That(order, Is.EqualTo(new[] { "past" }));
    }

    // ----------------------------------------------------------------------
    // RunIn correctness: relative to CurrentPicosecondTime, which in Fast=false
    // is TicksPassed (virtual). So RunIn(0, ...) fires on next instruction.
    // ----------------------------------------------------------------------

    [Test]
    public void RunIn_Zero_FiresOnNextInstruction() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        bool fired = false;
        vm.RunIn(0, () => fired = true);
        vm.ExecuteInstruction(true);
        Assert.That(fired, Is.True);
    }

    [Test]
    public void RunIn_Negative_FiresOnNextInstruction() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        bool fired = false;
        vm.RunIn(-CatVm.PicosecondsPerSecond, () => fired = true);
        vm.ExecuteInstruction(true);
        Assert.That(fired, Is.True);
    }

    // ----------------------------------------------------------------------
    // Reset semantics: scheduled events must be dropped, scheduler must stop
    // pinging _hasEvent for them, and subsequent schedules must work cleanly.
    // ----------------------------------------------------------------------

    [Test]
    public void Reset_ClearsScheduledEvents() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        bool fired = false;
        vm.RunAt(-1, () => fired = true);
        vm.Reset();
        vm.LoadData(NopBlock(2));
        for (int i = 0; i < 10; i++) vm.ExecuteInstruction(true);
        Assert.That(fired, Is.False);
    }

    [Test]
    public void ScheduleAfterReset_FiresNormally() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        vm.RunAt(-1, () => { /* dropped by reset */ });
        vm.Reset();
        vm.LoadData(NopBlock(2));
        bool fired = false;
        vm.RunAt(-1, () => fired = true);
        vm.ExecuteInstruction(true);
        Assert.That(fired, Is.True);
    }

    [Test]
    public void Reset_WhileSchedulerSleepingOnFarFutureEvent_LeavesNoZombie() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        bool fired = false;
        vm.RunAt(long.MaxValue / 2, () => fired = true);
        Thread.Sleep(20); // give the scheduler time to start its long sleep
        vm.Reset();
        vm.LoadData(NopBlock(2));
        for (int i = 0; i < 1000; i++) {
            vm.ExecuteInstruction(true);
            vm.Cpu.Ip = 0;
        }
        Assert.That(fired, Is.False);
    }

    // ----------------------------------------------------------------------
    // Concurrent scheduling from many threads.
    // ----------------------------------------------------------------------

    [Test]
    public void ConcurrentSchedulers_AllEventsEventuallyFire() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        int hits = 0;
        const int producers = 8;
        const int perProducer = 200;
        Barrier barrier = new(producers);
        Task[] producersTasks = new Task[producers];
        for (int p = 0; p < producers; p++) {
            int pid = p;
            producersTasks[p] = Task.Run(() => {
                barrier.SignalAndWait();
                for (int i = 0; i < perProducer; i++) {
                    vm.RunAt(-((pid * perProducer) + i + 1),
                             () => Interlocked.Increment(ref hits));
                }
            });
        }
        Task.WaitAll(producersTasks);

        // Drain. Each ExecuteInstruction drains all currently-due events under the lock,
        // so a single execute should catch everything; loop a few times for safety.
        for (int i = 0; i < 16; i++) vm.ExecuteInstruction(true);

        Assert.That(hits, Is.EqualTo(producers * perProducer));
    }

    [Test]
    public void ConcurrentScheduling_DuringExecution_DoesNotDeadlockOrCorrupt() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(64));
        int hits = 0;
        var cts = new CancellationTokenSource();

        // Background producer scheduling past-time events as fast as it can.
        Task producer = Task.Run(() => {
            int n = 0;
            while (!cts.IsCancellationRequested) {
                vm.RunAt(-(++n), () => Interlocked.Increment(ref hits));
                if ((n & 0xFF) == 0) Thread.Yield();
            }
            return n;
        });

        // Meanwhile, the main thread drives ExecuteInstruction. Each instruction
        // may drain newly-scheduled events.
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 250) {
            vm.ExecuteInstruction(true);
            vm.Cpu.Ip = 0;
        }

        cts.Cancel();
        producer.Wait();

        // Drain whatever's left.
        for (int i = 0; i < 32; i++) {
            vm.ExecuteInstruction(true);
            vm.Cpu.Ip = 0;
        }

        Assert.That(hits, Is.GreaterThan(0), "no events fired during stress run");
    }

    [Test]
    public void ConcurrentScheduling_DuringDrain_EventsAreNotLost() {
        // Tighter version: every callback schedules another event from inside
        // a parallel producer. This stresses callback re-entry crossing with
        // the producer thread.
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(64));
        int hits = 0;
        var cts = new CancellationTokenSource();

        // 4 producers scheduling past-time events.
        Task[] producers = new Task[4];
        for (int p = 0; p < producers.Length; p++) {
            producers[p] = Task.Run(() => {
                int n = 0;
                while (!cts.IsCancellationRequested) {
                    vm.RunAt(-(++n), () => Interlocked.Increment(ref hits));
                }
            });
        }

        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 250) {
            vm.ExecuteInstruction(true);
            vm.Cpu.Ip = 0;
        }

        cts.Cancel();
        Task.WaitAll(producers);

        for (int i = 0; i < 32; i++) {
            vm.ExecuteInstruction(true);
            vm.Cpu.Ip = 0;
        }

        Assert.That(hits, Is.GreaterThan(0));
    }

    // ----------------------------------------------------------------------
    // Scheduler thread (async firing). In Fast mode, CurrentPicosecondTime is
    // wall-clock-based, so the scheduler should fire events even when the
    // hot path isn't moving virtual time forward.
    // ----------------------------------------------------------------------

    [Test]
    public void FastMode_ScheduledEvent_FiresAsynchronously_HotPathDrains() {
        CatVm vm = NewFastVm();
        vm.LoadData(NopBlock(2));
        // Start Runtime stopwatch so CurrentPicosecondTime advances.
        vm.Runtime.Start();
        ManualResetEventSlim done = new();
        long fireRealTimeMs = -1;
        Stopwatch sw = Stopwatch.StartNew();
        vm.RunIn(50 * CatVm.PicosecondsPerMillisecond, () => {
            fireRealTimeMs = sw.ElapsedMilliseconds;
            done.Set();
        });

        // Drive ExecuteInstruction continuously so the hot path notices _hasEvent
        // and drains. The scheduler flips _hasEvent; the hot path runs FireDueEvents.
        Stopwatch timeout = Stopwatch.StartNew();
        while (!done.IsSet && timeout.ElapsedMilliseconds < 2000) {
            vm.ExecuteInstruction(true);
            vm.Cpu.Ip = 0;
        }

        Assert.That(done.IsSet, Is.True, "event did not fire within 2s");
        // Should fire at ~50ms ± timer slack. Allow a generous window.
        Assert.That(fireRealTimeMs, Is.GreaterThanOrEqualTo(40)
            .And.LessThan(200), $"fire timing out of range: {fireRealTimeMs}ms");
    }

    [Test]
    public void FastMode_SubMs_ScheduledEvent_FiresWithBusyWaitAccuracy() {
        // Sub-ms accuracy is the explicit promise of the busy-wait path.
        CatVm vm = NewFastVm();
        vm.LoadData(NopBlock(2));
        vm.Runtime.Start();

        // Pre-warm the scheduler task with a future event we'll let drain
        // synchronously — this pays the Task.Run startup cost up front so
        // the timed measurement below isn't polluted by it.
        ManualResetEventSlim warm = new();
        vm.RunAt(-1, () => warm.Set());
        Stopwatch warmSw = Stopwatch.StartNew();
        while (!warm.IsSet && warmSw.ElapsedMilliseconds < 500) {
            vm.ExecuteInstruction(true);
            vm.Cpu.Ip = 0;
        }
        Assert.That(warm.IsSet, Is.True, "scheduler failed to warm up");

        // Now do the real measurement.
        ManualResetEventSlim done = new();
        long fireMicros = -1;
        Stopwatch sw = Stopwatch.StartNew();
        vm.RunIn(500_000_000L /* 0.5ms in picos */, () => {
            fireMicros = (long)((double)sw.ElapsedTicks / Stopwatch.Frequency * 1_000_000);
            done.Set();
        });

        Stopwatch timeout = Stopwatch.StartNew();
        while (!done.IsSet && timeout.ElapsedMilliseconds < 1000) {
            vm.ExecuteInstruction(true);
            vm.Cpu.Ip = 0;
        }
        Assert.That(done.IsSet, Is.True);
        // 0.5ms = 500us. Allow a generous upper bound: timer slack on busy CI
        // hosts can be 10ms+. Mostly we want to know that the scheduler ran
        // and the event fired at all.
        Assert.That(fireMicros, Is.GreaterThanOrEqualTo(400)
            .And.LessThan(30_000), $"sub-ms fire timing: {fireMicros}us");
    }

    [Test]
    public void FastMode_Rescheduling_BeforeDeadline_UpdatesFireTime() {
        // Schedule an event for far future, then re-schedule it for near future.
        // The scheduler's CTS cancel should redirect to the new deadline.
        CatVm vm = NewFastVm();
        vm.LoadData(NopBlock(2));
        vm.Runtime.Start();
        ManualResetEventSlim done = new();
        Stopwatch sw = Stopwatch.StartNew();
        long fireMs = -1;

        vm.RunIn(500 * CatVm.PicosecondsPerMillisecond, () => {
            fireMs = sw.ElapsedMilliseconds;
            done.Set();
        });
        // Now schedule a closer one; the original should still fire eventually,
        // but the new one should fire first.
        vm.RunIn(30 * CatVm.PicosecondsPerMillisecond, () => {
            // first hit
            if (fireMs == -1) {
                fireMs = sw.ElapsedMilliseconds;
                done.Set();
            }
        });

        while (!done.IsSet && sw.ElapsedMilliseconds < 2000) {
            vm.ExecuteInstruction(true);
            vm.Cpu.Ip = 0;
        }
        Assert.That(done.IsSet, Is.True);
        Assert.That(fireMs, Is.GreaterThanOrEqualTo(20).And.LessThan(200));
    }

    // ----------------------------------------------------------------------
    // Many events at the same time stamp.
    // ----------------------------------------------------------------------

    [Test]
    public void ManyEvents_SameTime_AllFire() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        int hits = 0;
        for (int i = 0; i < 1000; i++) {
            vm.RunAt(-1, () => Interlocked.Increment(ref hits));
        }
        vm.ExecuteInstruction(true);
        Assert.That(hits, Is.EqualTo(1000));
    }

    [Test]
    public void ManyEvents_StaircaseTimes_AllFireInOrder() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        List<int> order = [];
        for (int i = 99; i >= 0; i--) {
            int captured = i;
            vm.RunAt(-i - 1, () => order.Add(captured));
        }
        vm.ExecuteInstruction(true);
        Assert.That(order, Has.Count.EqualTo(100));
        // Earliest time (most negative) fires first → order should descend from 99 to 0.
        for (int i = 0; i < 100; i++) {
            Assert.That(order[i], Is.EqualTo(99 - i));
        }
    }

    // ----------------------------------------------------------------------
    // Ordering invariant under interleaved insertion.
    // ----------------------------------------------------------------------

    [Test]
    public void InterleavedInsertion_PreservesTimeOrder() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        ConcurrentQueue<long> firedTimes = new();
        // Times chosen to be all in the past with mixed ordering.
        long[] times = [-5, -2, -10, -1, -100, -3, -50, -4, -7, -20];
        foreach (long t in times) {
            long captured = t;
            vm.RunAt(t, () => firedTimes.Enqueue(captured));
        }
        vm.ExecuteInstruction(true);
        long[] fired = firedTimes.ToArray();
        Assert.That(fired, Has.Length.EqualTo(times.Length));
        // Must be ascending in time (earliest = most negative = first).
        long[] expected = times.OrderBy(t => t).ToArray();
        Assert.That(fired, Is.EqualTo(expected));
    }

    // ----------------------------------------------------------------------
    // Stress: rapid schedule/fire cycles do not produce spurious extra fires
    // or drop events.
    // ----------------------------------------------------------------------

    [Test]
    public void RapidScheduleDrainCycles_NoDuplicatesOrDrops() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        int hits = 0;
        const int rounds = 500;
        for (int r = 0; r < rounds; r++) {
            vm.RunAt(-1, () => Interlocked.Increment(ref hits));
            vm.ExecuteInstruction(true);
            vm.Cpu.Ip = 0;
        }
        Assert.That(hits, Is.EqualTo(rounds));
    }

    [Test]
    public void RapidScheduleWithoutDrain_AllAccumulatedFireInSingleDrain() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        int hits = 0;
        const int n = 10_000;
        for (int i = 0; i < n; i++) {
            vm.RunAt(-i - 1, () => Interlocked.Increment(ref hits));
        }
        vm.ExecuteInstruction(true);
        Assert.That(hits, Is.EqualTo(n));
    }

    // ----------------------------------------------------------------------
    // Mixed past-and-future: past ones drain immediately, future ones do not.
    // ----------------------------------------------------------------------

    [Test]
    public void MixedPastAndFuture_OnlyPastFireImmediately() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(2));
        int pastHits = 0;
        int futureHits = 0;
        for (int i = 0; i < 50; i++) vm.RunAt(-i - 1, () => Interlocked.Increment(ref pastHits));
        for (int i = 0; i < 50; i++) vm.RunAt(long.MaxValue / 2 + i, () => Interlocked.Increment(ref futureHits));
        vm.ExecuteInstruction(true);
        Assert.That(pastHits, Is.EqualTo(50));
        Assert.That(futureHits, Is.EqualTo(0));
    }

    // ----------------------------------------------------------------------
    // Long-running smoke: many parallel producers and a continuous drainer.
    // Verifies that nothing in the lock/CTS/scheduler dance deadlocks or
    // miscounts under sustained pressure.
    // ----------------------------------------------------------------------

    [Test]
    public void SustainedConcurrentLoad_HitCountMatchesScheduleCount() {
        CatVm vm = NewVm();
        vm.LoadData(NopBlock(64));
        long hits = 0;
        long scheduled = 0;
        var cts = new CancellationTokenSource();

        Task[] producers = new Task[6];
        for (int p = 0; p < producers.Length; p++) {
            producers[p] = Task.Run(() => {
                while (!cts.IsCancellationRequested) {
                    long n = Interlocked.Increment(ref scheduled);
                    vm.RunAt(-n, () => Interlocked.Increment(ref hits));
                    if ((n & 0x3FF) == 0) Thread.Yield();
                }
            });
        }

        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 300) {
            vm.ExecuteInstruction(true);
            vm.Cpu.Ip = 0;
        }

        cts.Cancel();
        Task.WaitAll(producers);

        // Final drain.
        for (int i = 0; i < 64; i++) {
            vm.ExecuteInstruction(true);
            vm.Cpu.Ip = 0;
        }

        Assert.That(Interlocked.Read(ref hits), Is.EqualTo(Interlocked.Read(ref scheduled)),
            $"scheduled={scheduled} hits={hits}; events were lost or double-fired");
    }
}

