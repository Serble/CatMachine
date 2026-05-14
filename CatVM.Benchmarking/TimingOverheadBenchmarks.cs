using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace CatVM.Benchmarking;

// Benchmarks for the per-instruction overhead introduced by NON-Fast mode in
// CatVm.ExecuteInstruction. Designed to isolate each cost source so the timing
// code can be optimised.
//
// What the non-fast path adds on top of the fast path (see CatVm.cs around the
// `if (fast) return;` line):
//
//   long sleepNeeded = TicksPassed - Runtime.Elapsed.Ticks * PicosecondsPerTick;
//   if      (sleepNeeded > 1ms)        Thread.Sleep(...)
//   else if (sleepNeeded < -100ms)     Console.WriteLine(...)  // rate-limited to 1/s
//
// The dominant cost is `Runtime.Elapsed.Ticks` — a Stopwatch property that
// funnels into Stopwatch.GetTimestamp() and, on most platforms, a
// QueryPerformanceCounter / clock_gettime syscall (~15-30 ns).
//
// Separately, the event-loop short-circuit at the top of ExecuteInstruction:
//
//   if (_nextEvent != long.MaxValue && CurrentPicosecondTime >= _nextEvent) ...
//
// reads CurrentPicosecondTime — which in Fast mode is also Runtime.Elapsed.Ticks.
// Whenever any event is scheduled (even in the far future), every
// ExecuteInstruction pays an extra Stopwatch read regardless of `fast`. We
// measure that separately.
//
// Cross-reference matrix (all run a single NOP per invocation):
//
//                              | no event scheduled | distant event scheduled
//   --------------------------- |--------------------|------------------------
//   Fast=true                   | A                  | B
//   Fast=false                  | C                  | D
//
//   B - A   = cost of the `_nextEvent != MaxValue` branch + CurrentPicosecondTime read
//   C - A   = cost of the non-fast `sleepNeeded` calculation (one Runtime.Elapsed.Ticks)
//   D - C   = same as B - A
//   D - B   = same as C - A
//
// The synthetic baselines (StopwatchElapsedTicks, StopwatchGetTimestamp,
// EmptyLoop) make it possible to attribute the gap to the syscall vs the
// surrounding arithmetic.
//
// Picking cyclesPerSecond and iteration count:
//   * The non-fast path keeps TicksPassed and Runtime.Elapsed.Ticks in sync.
//     Whenever virtual time outruns real time by >1ms, Thread.Sleep fires —
//     which we DON'T want, that would dwarf the measurement.
//   * With 1 GHz (PicosecondsPerCycle = 1000) a single NOP advances virtual
//     time by 1 ns. To stay under 1ms even at the START of an iteration (the
//     critical moment, since iteration 0 has the largest |sleepNeeded| if the
//     stopwatch isn't started, or the smallest if it is), we Runtime.Restart()
//     in IterationSetup. Real time will then dominate virtual time throughout,
//     keeping sleepNeeded negative (the warning branch); the warning is rate
//     limited to once per real second so it amortises away.
//   * Crucially: if Runtime is NOT started, Runtime.Elapsed.Ticks == 0, so
//     sleepNeeded == TicksPassed grows monotonically and triggers Thread.Sleep
//     after ~1000 instructions. CatVm.Reset() (post-fix) calls Runtime.Reset()
//     which STOPS the stopwatch — we must Runtime.Start() / Restart() in
//     IterationSetup to get accurate measurements.
[SimpleJob(RunStrategy.Throughput)]
public class TimingOverheadBenchmarks {
    private const byte OpNop = 0x4D;

    // CatVm.Fast is init-only, so the Fast/non-Fast variants need separate
    // instances. Now that Reset() clears _events and resets Runtime, two VMs
    // are enough — events get re-scheduled in IterationSetup as needed.
    private CatVm _vmFast = null!;
    private CatVm _vmSlow = null!;

    [GlobalSetup]
    public void Setup() {
        _vmFast = new CatVm(Program.VmMemory, 1_000_000_000) { Fast = true };
        _vmSlow = new CatVm(Program.VmMemory, 1_000_000_000) { Fast = false };
        Stopwatch.StartNew();
    }

    // Common per-iteration prep: reload program, start the runtime stopwatch
    // (Reset() leaves it stopped), and optionally schedule a never-firing event.
    private static void PrimeIteration(CatVm vm, bool scheduleEvent) {
        vm.Reset();
        vm.LoadData([OpNop]);
        vm.Runtime.Start();
        if (scheduleEvent) {
            vm.RunAt(long.MaxValue / 2, static () => { });
        }
    }

    // ---------------------------------------------------------------------
    // Cell A — Fast, no events.
    // The "ideal" hot path: short-circuits the event check on _nextEvent,
    // skips the sleepNeeded calculation entirely.
    // ---------------------------------------------------------------------
    [IterationSetup(Target = nameof(Fast_NoEvent))]
    public void SetupFastNoEvent() => PrimeIteration(_vmFast, scheduleEvent: false);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void Fast_NoEvent() {
        for (int i = 0; i < Program.InstructionIterations; i++) {
            _vmFast.ExecuteInstruction(true);
            _vmFast.Cpu.Ip = 0;
        }
    }

    // ---------------------------------------------------------------------
    // Cell B — Fast, with a far-future event scheduled.
    // _nextEvent != long.MaxValue so the short-circuit FAILS and every tick
    // pays the cost of evaluating `CurrentPicosecondTime >= _nextEvent`.
    // In Fast mode CurrentPicosecondTime hits Runtime.Elapsed.Ticks, so this
    // measures one Stopwatch read per instruction on top of cell A.
    // ---------------------------------------------------------------------
    [IterationSetup(Target = nameof(Fast_DistantEvent))]
    public void SetupFastDistantEvent() => PrimeIteration(_vmFast, scheduleEvent: true);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void Fast_DistantEvent() {
        for (int i = 0; i < Program.InstructionIterations; i++) {
            _vmFast.ExecuteInstruction(true);
            _vmFast.Cpu.Ip = 0;
        }
    }

    // ---------------------------------------------------------------------
    // Cell C — Non-Fast, no events.
    // The straight-line non-fast cost: post-instruction sleepNeeded calculation
    // (one Runtime.Elapsed.Ticks + a multiply, a subtract, two branches that
    // both miss). No event short-circuit overhead.
    // ---------------------------------------------------------------------
    [IterationSetup(Target = nameof(NonFast_NoEvent))]
    public void SetupNonFastNoEvent() => PrimeIteration(_vmSlow, scheduleEvent: false);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void NonFast_NoEvent() {
        for (int i = 0; i < Program.InstructionIterations; i++) {
            _vmSlow.ExecuteInstruction(false);
            _vmSlow.Cpu.Ip = 0;
        }
    }

    // ---------------------------------------------------------------------
    // Cell D — Non-Fast, with a far-future event scheduled.
    // Worst case: TWO Stopwatch reads per instruction. One for the event
    // short-circuit at the top of ExecuteInstruction, one for sleepNeeded
    // at the bottom. This is what optimisations should chase first — if you
    // can deduplicate the two reads in the same call, this falls to (C).
    // ---------------------------------------------------------------------
    [IterationSetup(Target = nameof(NonFast_DistantEvent))]
    public void SetupNonFastDistantEvent() => PrimeIteration(_vmSlow, scheduleEvent: true);

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void NonFast_DistantEvent() {
        for (int i = 0; i < Program.InstructionIterations; i++) {
            _vmSlow.ExecuteInstruction(false);
            _vmSlow.Cpu.Ip = 0;
        }
    }

    // ---------------------------------------------------------------------
    // Input-device simulation — frequent events firing.
    //
    // Models a workload like a 10 kHz input device (mouse / gamepad polling /
    // periodic timer interrupt) where a callback fires every `IntervalPicos`
    // of *virtual* time and immediately reschedules itself. This exercises
    // the synchronous drain path (`_hasEvent` set, `FireDueEvents` runs) at
    // a realistic-ish duty cycle, not the synthetic "every instruction" or
    // "never" extremes covered above.
    //
    // What this measures, compared to (D) NonFast_DistantEvent:
    //   * Same per-instruction cost (event check + sleepNeeded calc) when
    //     no event is due, PLUS
    //   * Amortised cost of: _hasEvent flag flip, lock acquisition,
    //     remove-from-tail, callback invocation, RunIn re-scheduling
    //     (which re-sorts + cancels CTS + wakes the scheduler task)
    //     every `IntervalPicos` virtual time units.
    //
    // With cyclesPerSecond = 1 GHz, 1 instruction = 1 ns of virtual time =
    // 1000 picoseconds. IntervalPicos = 100_000 = 100 ns = 100 instructions
    // between fires ⇒ ~200_000 fires over a 20M-instruction iteration.
    //
    // The Fast variant uses real wall-clock time for CurrentPicosecondTime
    // and is much slower — Stopwatch reads are still expensive, and the
    // scheduler task contends for the lock during each reschedule. That
    // benchmark mostly measures lock/CTS thrash; we use a longer interval
    // so it's not completely dominated by Task.Run / SpinWait startup.
    // ---------------------------------------------------------------------
    private const long FrequentEventIntervalPicos = 100_000;  // 100 ns of virtual time
    private const long FrequentEventIntervalPicosFast = 10_000_000;  // 10 us of real time
    private Action _frequentRescheduleNonFast = null!;
    private Action _frequentRescheduleFast = null!;
    public long FrequentEventHits;  // public so JIT can't elide writes

    [IterationSetup(Target = nameof(NonFast_FrequentEvents))]
    public void SetupNonFastFrequentEvents() {
        PrimeIteration(_vmSlow, scheduleEvent: false);
        // Cache the delegate so we don't measure delegate allocation overhead
        // inside the hot loop.
        _frequentRescheduleNonFast = () => {
            FrequentEventHits++;
            _vmSlow.RunIn(FrequentEventIntervalPicos, _frequentRescheduleNonFast);
        };
        FrequentEventHits = 0;
        _vmSlow.RunIn(FrequentEventIntervalPicos, _frequentRescheduleNonFast);
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void NonFast_FrequentEvents() {
        for (int i = 0; i < Program.InstructionIterations; i++) {
            _vmSlow.ExecuteInstruction(false);
            _vmSlow.Cpu.Ip = 0;
        }
    }

    [IterationSetup(Target = nameof(Fast_FrequentEvents))]
    public void SetupFastFrequentEvents() {
        PrimeIteration(_vmFast, scheduleEvent: false);
        _frequentRescheduleFast = () => {
            FrequentEventHits++;
            _vmFast.RunIn(FrequentEventIntervalPicosFast, _frequentRescheduleFast);
        };
        FrequentEventHits = 0;
        _vmFast.RunIn(FrequentEventIntervalPicosFast, _frequentRescheduleFast);
    }

    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public void Fast_FrequentEvents() {
        for (int i = 0; i < Program.InstructionIterations; i++) {
            _vmFast.ExecuteInstruction(true);
            _vmFast.Cpu.Ip = 0;
        }
    }

    // ---------------------------------------------------------------------
    // Synthetic baselines (no VM involvement). These are NOT meant to be
    // compared with the cells above directly — they isolate the cost of
    // individual primitives so deltas between (A)..(D) can be attributed.
    // ---------------------------------------------------------------------

    // Pure for-loop + counter increment overhead. Subtract this from every
    // other benchmark to get the per-instruction cost. (BenchmarkDotNet's
    // own overhead measurement also covers this, but having it explicit
    // helps when reading the report side-by-side.)
    [Benchmark(OperationsPerInvoke = Program.InstructionIterations)]
    public int EmptyLoop() {
        int sum = 0;
        for (int i = 0; i < Program.InstructionIterations; i++) {
            sum++;
        }
        return sum;
    }
}
