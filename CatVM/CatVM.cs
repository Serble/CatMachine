using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CatVM.Ops;
using CatVM.Serial;

namespace CatVM;

/// <summary>
/// A virtual machine instance that executions instructions stored in its memory.
/// <p/>
/// See <see cref="LoadData"/> and <see cref="CatVM(int, uint, byte[])"/>.
/// </summary>
public class CatVM {
    /// <summary>
    /// In benchmark mode the VM will print out the instructions per second
    /// every 10 million instructions.
    /// </summary>
    public const bool BenchmarkMode = false;
    
    /// <summary>
    /// Debug mode enables extra checks.
    /// <see cref="ErrorOnRomWrite"/>, <see cref="DisallowedWriteRegions"/>, and <see cref="DisallowedReadRegions"/>
    /// will be enforced when debug mode is enabled.
    /// </summary>
    public const bool DebugMode = false;
    
    // Time constants
    public const long PicosecondsPerSecond = 1000000000000L;
    public const long PicosecondsPerTick = 100000L;
    public const long PicosecondsPerMillisecond = 1000000000L;

#region Parameters
    
    /// <summary>
    /// The physical memory of the VM. Indices into this array are *physical* addresses.
    /// Guest accesses go through <see cref="Translate"/> first when Virtual Mode is on; when
    /// Virtual Mode is off the guest's address is the physical address (identity mapping),
    /// so kernel code and external consumers can index this array directly without any
    /// performance penalty.
    /// <para/>Be careful when modifying this directly — bypasses translation and bounds checks.
    /// </summary>
    public byte[] Memory = null!;
    
    /// <summary>
    /// A copy of the original ROM data. This is used to reset the VM.
    /// </summary>
    public byte[] Rom { get; set; }
    
    /// <summary>
    /// Whether hardware interrupts are enabled. If false, hardware interrupts will
    /// still be added to the queue but will not be handled until this is set to true.
    /// </summary>
    public bool InterruptsEnabled { get; set; } = true;
    
    /// <summary>
    /// Number of picoseconds that pass for each CPU cycle. This is calculated based on the cycles per second
    /// passed in the constructor, but can be modified at runtime if needed.
    /// </summary>
    public long PicosecondsPerCycle { get; set; }
    
    /// <summary>
    /// The number of CPU cycles that occur each second. This is calculated based on the picoseconds per cycle,
    /// if modified at runtime the picoseconds per cycle will be updated accordingly.
    /// </summary>
    public uint CyclesPerSecond {
        get => (uint)(PicosecondsPerSecond / PicosecondsPerCycle);
        set => PicosecondsPerCycle = PicosecondsPerSecond / value;
    }
    
    /// <summary>
    /// Whether to throw an error when the program attempts to write to the ROM.
    /// <remarks>This is only enforced in debug mode.</remarks>
    /// </summary>
    public bool ErrorOnRomWrite { get; set; }
    
    /// <summary>
    /// Whether special testing interrupts (0x90-0x9F) are enabled. These interrupts are only
    /// intended for debugging and testing. If disabled they will be treated as normal user defined interrupts.
    /// </summary>
    public bool EnableTestingInterrupts { get; set; }
    
    /// <summary>
    /// Whether to print out exceptions that occur during instruction execution.
    /// This can be useful for debugging, but may have a performance impact.
    /// <remarks>This will print out errors that were handled correctly by the VM.</remarks>
    /// </summary>
    public bool DumpErrors { get; set; }
    
    /// <summary>
    /// Regions of memory that will trigger an error upon being written to.
    /// <remarks>Only available in <see cref="DebugMode"/>.</remarks>
    /// </summary>
    public (uint start, uint length)[] DisallowedWriteRegions { get; set; } = [];
    
    /// <summary>
    /// Regions of memory that will trigger an error upon being read from.
    /// <remarks>Only available in <see cref="DebugMode"/>.</remarks>
    /// </summary>
    public (uint start, uint length)[] DisallowedReadRegions { get; set; } = [];
    
    /// <summary>
    /// Low level handle to the memory array.
    /// This can be used for advanced operations like pinning the memory for use with unmanaged code.
    /// </summary>
    public GCHandle? MemoryHandle { get; private set; }
    
    /// <summary>
    /// Queue of pending hardware interrupts. Hardware interrupts can be added to this queue using
    /// <see cref="HardwareInterrupt(byte)"/> or <see cref="HardwareInterrupt(SpecialInterrupts)"/>.
    /// </summary>
    private ConcurrentQueue<byte> HardwareInterruptQueue { get; } = [];

    // best guess at how many interrupts are pending
    // use to avoid expensive queue checks.
    private int _hardwareInterruptAproxCount;

    /// <summary>
    /// Number of virtual picoseconds that have passed since the VM started.
    /// <remarks>This is in 'machine' time, not real world time.</remarks>
    /// </summary>
    public long TicksPassed { get; private set; } // This isn't real time this is virtual time, 1 tick = 1 picosecond
    
    /// <summary>
    /// Real time stopwatch for the VM. This is used to keep track of how much real time has passed,
    /// it gets paused when the VM is paused to prevent time from passing while the VM is not running.
    /// </summary>
    public Stopwatch Runtime { get; } = new();
    
    /// <summary>
    /// Whether to skip the timing and sleep logic in the main loop to run as fast as possible.
    /// </summary>
    public bool Fast { get; init; }
    
    /// <summary>
    /// The virtual CPU state of the VM, including registers and flags.
    /// This is used by instructions to read and modify the CPU state.
    /// <p/>
    /// Be careful when modifying this directly. It is not recommended.
    /// </summary>
    public CatCpuState Cpu;
    
    /// <summary>
    /// Whether the VM is currently paused. While paused the VM will not execute instructions
    /// and time will not pass.
    /// </summary>
    public bool Paused {
        get;
        set {
            field = value;
            if (value) {
                Runtime.Stop();
            } else {
                Runtime.Start();
            }
        }
    }

    /// <summary>
    /// Mapping of serial port numbers to devices.
    /// <p/>
    /// Ports 0-15 are reserved for system use (like display and input), so user devices should start at port 16.
    /// Additionally, directly writing to this dictionary is not recommended,
    /// please use <see cref="RegisterSerialDevice(uint, ISerialDevice)"/> and
    /// <see cref="RegisterSerialDevice(ISerialDevice)"/> to ensure ports are not accidentally overwritten.
    /// </summary>
    public Dictionary<uint, ISerialDevice> SerialDevices { get; } = [];
    
#endregion
    
    private readonly int _memoryBytes;
    private DateTime _lastSlowWarning = DateTime.MinValue;

    private List<(long time, Action callback)> _events = [];
    private long _nextEvent = long.MaxValue;
    
    public CatVM(int memoryBytes, uint cyclesPerSecond, byte[]? rom = null) {
        _memoryBytes = memoryBytes;
        Rom = rom ?? [];
        PicosecondsPerCycle = PicosecondsPerSecond / cyclesPerSecond;

        if (memoryBytes < Rom.Length) {
            throw new Exception($"Not enough memory for Rom, needed: {Rom.Length}, got: {memoryBytes}");
        }
        
        Reset();
    }

    /// <summary>
    /// Reset the virtual machine to its initial state.
    /// This is designed to represent a physical reset button.
    /// </summary>
    /// <param name="preserveMem">Whether to not wipe the memory.</param>
    public void Reset(bool preserveMem = false) {
        Cpu = new CatCpuState();
        if (!preserveMem) {
            MemoryHandle?.Free();   // Release old memory array
            MemoryHandle = null;
            Memory = new byte[_memoryBytes];
            MemoryHandle = GCHandle.Alloc(Memory, GCHandleType.Pinned);
        }
        
        // get offset for display buffer (it will go at the end of memory)
        Cpu.Sp = (uint)_memoryBytes;  // end of regular memory (non display buffer)
        Cpu.MLen = (uint)_memoryBytes;
        
        if (Rom.Length > 0) {
            LoadData(Rom);
        }
    }
    
    /// <summary>
    /// Places the specified data into the VM's memory starting at the specified address.
    /// </summary>
    /// <param name="data">The data to load into memory.</param>
    /// <param name="address">The address to place it into.</param>
    /// <exception cref="Exception">When the address is out of bounds.</exception>
    public void LoadData(byte[] data, uint address = 0) {
        if (address + data.Length > Memory.Length) {
            throw new Exception("ROM exceeds memory bounds.");
        }
        Array.Copy(data, 0, Memory, address, data.Length);
    }
    
    /// <summary>
    /// Register a serial device on a specific port.
    /// If the port is already in use, an exception will be thrown.
    /// </summary>
    /// <param name="port">The port to use.</param>
    /// <param name="device">The device to register.</param>
    /// <exception cref="Exception">When the requested port is already in use.</exception>
    public void RegisterSerialDevice(uint port, ISerialDevice device) {
        if (!SerialDevices.TryAdd(port, device)) {
            throw new Exception($"Serial port {port} is already in use.");
        }
    }

    /// <summary>
    /// Register a serial device on the next available unreserved port (starting at 16).
    /// Ports 0-15 are reserved for system use (like display and input).
    /// </summary>
    /// <param name="device"></param>
    public void RegisterSerialDevice(ISerialDevice device) {
        uint port = 0;
        while (SerialDevices.ContainsKey(port)) {
            port++;
        }
        SerialDevices[port] = device;
    }
    
    /// <summary>
    /// Get the serial device registered to a specific port,
    /// or <see cref="ISerialDevice.Null"/> if no device is registered on that port.
    /// </summary>
    /// <param name="port">The port to get the serial device for.</param>
    /// <returns>The requested serial device.</returns>
    public ISerialDevice GetSerialDevice(uint port) {
        return SerialDevices.GetValueOrDefault(port, ISerialDevice.Null);
    }

    /// <summary>
    /// Privilege gate for opcodes that may only run in kernel mode.
    /// In v1 "kernel mode" means simply <c>!VirtualMode</c>; the
    /// SupervisorMode bit is reserved for a future driver tier and is
    /// not required here.
    /// Raises <see cref="SpecialInterrupts.ProtectionFault"/> and returns
    /// <c>false</c> when called from user (virtual) mode.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPrivileged() {
        if (!Cpu.VirtualMode || Cpu.SupervisorMode) {
            return true;
        }

        Interrupt(SpecialInterrupts.ProtectionFault);
        return false;
    }

#region Memory Read/Write Methods

    /// <summary>
    /// Translate a guest address to a physical address.
    /// <para/>
    /// When Virtual Mode is off (the common case for the kernel and all legacy programs)
    /// this is a single predictable branch and a return — the JIT inlines it to nothing on
    /// the hot path. When Virtual Mode is on, the address is bounds-checked against
    /// <c>MLen</c> and offset by <c>MBase</c>.
    /// </summary>
    /// <param name="addr">Guest (virtual) address.</param>
    /// <param name="size">Access width in bytes — used only for the upper-bound check.</param>
    /// <returns>Physical address that should be used to index <see cref="Memory"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint Translate(uint addr, uint size) {
        if ((Cpu.Mode & 1u) == 0u) {
            return addr;
        }
        return TranslateVirt(addr, size);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint TranslateVirt(uint addr, uint size) {
        uint end = addr + size;
        if (end < addr || end > Cpu.MLen) ThrowVirtOob(addr, size);
        return addr + Cpu.MBase;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowVirtOob(uint addr, uint size) =>
        throw new MemoryOutOfRange(false, addr, size, "Virtual mode bounds violation");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read8() {
        uint p = Translate(Cpu.Ip, 1);
        Cpu.Ip++;
        ValidateMemoryRead(p, 1);
        return Memory[p];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read8(uint ptr) {
        uint p = Translate(ptr, 1);
        ValidateMemoryRead(p, 1);
        return Memory[p];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort Read16() {
        uint p = Translate(Cpu.Ip, 2);
        ValidateMemoryRead(p, 2);
        ushort value = Unsafe.ReadUnaligned<ushort>(ref Memory[p]);
        Cpu.Ip += 2;
        return value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort Read16(uint ptr) {
        uint p = Translate(ptr, 2);
        ValidateMemoryRead(p, 2);
        return Unsafe.ReadUnaligned<ushort>(ref Memory[p]);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadWord() {
        uint p = Translate(Cpu.Ip, 4);
        ValidateMemoryRead(p, 4);
        uint value = Unsafe.ReadUnaligned<uint>(ref Memory[p]);
        Cpu.Ip += 4;
        return value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadWord(uint ptr) {
        uint p = Translate(ptr, 4);
        ValidateMemoryRead(p, 4);
        return Unsafe.ReadUnaligned<uint>(ref Memory[p]);
    }

    /// <summary>
    /// Read a byte from physical memory, bypassing virtual-mode translation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read8Physical(uint ptr) {
        ValidateMemoryRead(ptr, 1);
        return Memory[ptr];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Read16Physical(uint ptr) {
        ValidateMemoryRead(ptr, 2);
        return Unsafe.ReadUnaligned<ushort>(ref Memory[ptr]);
    }

    /// <summary>
    /// Read a 32-bit word from physical memory, bypassing virtual-mode translation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadWordPhysical(uint ptr) {
        ValidateMemoryRead(ptr, 4);
        return Unsafe.ReadUnaligned<uint>(ref Memory[ptr]);
    }
    
    public string ReadString() {
        List<byte> bytes = [];
        while (true) {
            byte b = Read8();
            if (b == 0) break;
            bytes.Add(b);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
    
    public string ReadString(uint ptr) {
        List<byte> bytes = [];
        while (true) {
            byte b = Read8(ptr++);
            if (b == 0) break;
            bytes.Add(b);
        }
        return Encoding.UTF8.GetString(bytes.ToArray());
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StackPush(uint value) {
        Cpu.Sp -= 4;
        uint p = Translate(Cpu.Sp, 4);
        ValidateMemoryWrite(p, 4);
        Unsafe.WriteUnaligned(ref Memory[p], value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StackPush(byte value) {
        Cpu.Sp -= 1;
        uint p = Translate(Cpu.Sp, 1);
        ValidateMemoryWrite(p, 1);
        Memory[p] = value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void StackPush(ushort value) {
        Cpu.Sp -= 2;
        uint p = Translate(Cpu.Sp, 2);
        ValidateMemoryWrite(p, 2);
        Unsafe.WriteUnaligned(ref Memory[p], value);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint StackPop() {
        uint p = Translate(Cpu.Sp, 4);
        ValidateMemoryRead(p, 4);
        uint value = Unsafe.ReadUnaligned<uint>(ref Memory[p]);
        Cpu.Sp += 4;
        return value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte StackPop8() {
        uint p = Translate(Cpu.Sp, 1);
        ValidateMemoryRead(p, 1);
        byte value = Memory[p];
        Cpu.Sp += 1;
        return value;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort StackPop16() {
        uint p = Translate(Cpu.Sp, 2);
        ValidateMemoryRead(p, 2);
        ushort value = Unsafe.ReadUnaligned<ushort>(ref Memory[p]);
        Cpu.Sp += 2;
        return value;
    }
    
#endregion
    
    /// <summary>
    /// Run the VM until the cancellation token is cancelled.
    /// This will execute instructions in a loop, handling interrupts and timing.
    /// </summary>
    /// <param name="cancellationToken">Token to signal to stop executing.</param>
    public void Run(CancellationToken? cancellationToken = null) {
        Runtime.Restart();
        
        int instructionsExecuted;
        if (BenchmarkMode) {
            Task.Run(() => {
                Stopwatch sw = Stopwatch.StartNew();
                int i = 0;
                while (true) {
                    i++;
                    if (i >= 10) {
                        i = 0;
                        sw.Restart();
                        instructionsExecuted = 0;
                    }
                    Thread.Sleep(1000);
                    int instructionsPerSecond = (int)(instructionsExecuted / sw.Elapsed.TotalSeconds);
                    if (BenchmarkMode) {
                        Console.WriteLine($"IPS: {instructionsPerSecond}");
                    }
                }
            });
        }

        // Main loop has try catch here to reduce overhead in ExecuteInstruction
        // but if it throws inside we need to continue, so double while loop.
        while (cancellationToken is not { IsCancellationRequested: true }) {
            ExecuteWithErrorHandling(() => {
                while (cancellationToken is not { IsCancellationRequested: true }) {
                    if (Paused) {
                        Thread.Yield();
                        continue;
                    }

                    ExecuteInstruction(Fast);
                    if (BenchmarkMode) {
                        instructionsExecuted++;
                    }
                }
            });
        }
    }

    /// <summary>
    /// Execute the provided action and handle errors related to <see cref="ExecuteInstruction"/>
    /// gracefully. This method is intended to wrap an execution loop.
    /// <p/>
    /// It is not part of <see cref="ExecuteInstruction"/> for performance reasons.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    public void ExecuteWithErrorHandling(Action action) {
        try {
            action();
        }
        catch (DivideByZeroException e) {
            DumpError(e);
            Interrupt(SpecialInterrupts.DivideByZero);
        }
        catch (MemoryOutOfRange e) {
            DumpError(e);
            try {
                Interrupt(SpecialInterrupts.PageFault);
            }
            catch (MemoryOutOfRange ex) {
                DumpError(ex);
                Interrupt(SpecialInterrupts.PageFault);
            }
        }
        catch (IndexOutOfRangeException e) {
            DumpError(e);
            
            // we need to check if this was due to an invalid opcode
            // check if the last stack frame was in ExecuteInstruction
            StackTrace trace = new(e);
            StackFrame[] frames = trace.GetFrames();
            if (frames.Length > 1 && frames[1].GetMethod()?.Name == nameof(ExecuteInstruction)) {
                // invalid opcode
                Interrupt(SpecialInterrupts.InvalidInstruction);
            }
            else {
                // some other index out of range (we'll assume with memory)
                Interrupt(SpecialInterrupts.PageFault);
            }
        }
        catch (ArgumentException e) {
            DumpError(e);
            Interrupt(SpecialInterrupts.PageFault);
        }
        catch (Exception e) {
            DumpError(e);
            Interrupt(SpecialInterrupts.InvalidInstruction);
        }
    }

    private void DumpError(Exception e) {
        if (DumpErrors) {
            Console.WriteLine(e);
        }
    }
    
    /// <summary>
    /// Execute a single instruction at the current instruction pointer.
    /// This method also handles timing and all interrupts.
    /// </summary>
    /// <param name="fast">Whether to ignore timings and run as fast as possible.</param>
    public unsafe void ExecuteInstruction(bool fast = false) {
        if (InterruptsEnabled
            && Volatile.Read(ref _hardwareInterruptAproxCount) > 0
            && HardwareInterruptQueue.TryDequeue(out byte hardwareInterrupt)) {
            HandleInterrupt(hardwareInterrupt);
            Interlocked.Decrement(ref _hardwareInterruptAproxCount);
        }

        // Short-circuit: when no events are scheduled (_nextEvent == long.MaxValue, the
        // steady state for any pure-throughput run) this avoids the QueryPerformanceCounter
        // syscall hidden in CurrentPicosecondTime via Stopwatch.Elapsed (~15-30ns/call).
        if (_nextEvent != long.MaxValue && CurrentPicosecondTime >= _nextEvent) {
            FireDueEvents();
        }
        
        byte opcode = Read8();
        
        // don't bounds check opcode because the array lookup
        // will do that for us and throw an IndexOutOfRangeException
        // (CpuExceptionTest.InvalidInstruction_PathDistinguishedFromGenericIndexOutOfRange
        //  relies on the IOOR throwing from inside this method).
        delegate*<CatVM, void> executor = OperationExecutors[opcode];
        int cycles = OperationCycles[opcode];
        executor(this);
        TicksPassed += cycles * PicosecondsPerCycle;
        
        if (fast) return;  // don't bother calculating anything if fast
        
        // wait the required time (sleepNeeded is in picoseconds)
        long sleepNeeded = TicksPassed - Runtime.Elapsed.Ticks * PicosecondsPerTick;
        
        // Thread.Sleep has a minimum time of 1ms
        if (sleepNeeded > PicosecondsPerMillisecond) {
            Thread.Sleep((int)(sleepNeeded / PicosecondsPerMillisecond));
        } else if (sleepNeeded < -100 * PicosecondsPerMillisecond && DateTime.Now - _lastSlowWarning > TimeSpan.FromMilliseconds(1000)) {
            Console.WriteLine($"VM is running {sleepNeeded / -PicosecondsPerMillisecond}ms behind!");
            _lastSlowWarning = DateTime.Now;
        }
    }

#region Interrupt Handling

    /// <summary>
    /// Send a software interrupt to the application.
    /// </summary>
    /// <param name="id">The interrupt code.</param>
    public void Interrupt(SpecialInterrupts id) => Interrupt((byte)id);
    
    /// <summary>
    /// Send a software interrupt to the application.
    /// </summary>
    /// <param name="id">The interrupt code.</param>
    public void Interrupt(byte id) {
        HandleInterrupt(id);
    }
    
    /// <summary>
    /// Send a hardware interrupt to the application.
    /// This will be added to the <see cref="HardwareInterruptQueue"/> and
    /// handled when possible.
    /// </summary>
    /// <param name="id">The interrupt code.</param>
    public void HardwareInterrupt(SpecialInterrupts id) => HardwareInterrupt((byte)id);
    
    /// <summary>
    /// Send a hardware interrupt to the application.
    /// This will be added to the <see cref="HardwareInterruptQueue"/> and
    /// handled when possible.
    /// </summary>
    /// <param name="id">The interrupt code.</param>
    public void HardwareInterrupt(byte id) {
        HardwareInterruptQueue.Enqueue(id);
        Interlocked.Add(ref _hardwareInterruptAproxCount, 1);
    }
    
    public void HandleInterrupt(byte id) {
        switch (id) {
            // 0x8X SYSTEM INTERRUPTS
            
            case 0x80: {  // print
                InterruptHandlers.PrintInterrupt(this);
                return;
            }

            case 0x81: {  // halt
                InterruptHandlers.HaltInterrupt(this);
                return;
            }
            
            case 0x82: {  // shutdown
                InterruptHandlers.ShutdownInterrupt(this);
                return;
            }
            
            case 0x83: {  // reset
                InterruptHandlers.ResetInterrupt(this);
                return;
            }
            
            case 0x85: {  // get uptime
                InterruptHandlers.GetUptimeInterrupt(this);
                return;
            }
            
            // 0x9X DEBUG INTERRUPTS
            
            case 0x90 when EnableTestingInterrupts: {  // print number
                InterruptHandlers.PrintNumInterrupt(this);
                return;
            }
        }
        
        // User defined interrupt (or default)
        if (Cpu.It == uint.MaxValue) {
            // no interrupt table, default handler
            InterruptHandlers.DefaultHandler(this, id);
            return;
        }
        
        // Find the handler. The IT lives in kernel space (a physical address) so bypass
        // translation so a user-mode interrupt can't redirect IT lookups through MBase.
        byte entryCount = Read8Physical(Cpu.It);

        uint entryPtr = Cpu.It + 1;
        for (int i = 0; i < entryCount; i++) {
            byte code = Read8Physical(entryPtr);
            uint handlerPtr = ReadWordPhysical(entryPtr + 1);
            if (code == id) {
                // found, build the appropriate frame and dispatch
                BuildInterruptFrameAndDispatch(handlerPtr);
                return;  // now executing the handler
            }

            entryPtr += 5;
        }
        
        // not found, default
        InterruptHandlers.DefaultHandler(this, id);
    }

    /// <summary>
    /// Markers pushed at the top of every interrupt frame.
    /// <see cref="Iret"/> consults this to decide what to restore.
    /// </summary>
    public const byte InterruptFrameMarkerKernel     = 0x00;
    public const byte InterruptFrameMarkerUser       = 0x01;
    public const byte InterruptFrameMarkerSupervisor = 0x02;

    /// <summary>
    /// Build the entry frame for an IT-resolved interrupt and jump into the handler.
    /// <para/>
    /// Both paths push a 1-byte marker last so <see cref="Iret"/> can dispatch uniformly.
    /// User -> kernel: switch <c>Sp</c> onto <c>Ksp</c>, clear VirtMode, push full frame.
    /// Kernel -> kernel: push only <c>Ip</c> + marker on the current kernel stack.
    /// Every handler returns via <c>iret</c>; <c>ret</c> is for <c>call</c>/<c>ret</c> only.
    /// </summary>
    private void BuildInterruptFrameAndDispatch(uint handlerPtr) {
        if ((Cpu.Mode & 1u) != 0u) {
            // Virtual mode (user or driver) -> kernel: full frame on the kernel stack.
            // Marker 0x01 = user (Mode=0b01), 0x02 = driver/supervisor-virtual (Mode=0b11).
            byte marker = Cpu.SupervisorMode
                ? InterruptFrameMarkerSupervisor
                : InterruptFrameMarkerUser;

            uint userSp = Cpu.Sp;
            uint userIp = Cpu.Ip;

            // Switch to canonical kernel mode (Mode=0) and onto Ksp BEFORE any push so the
            // pushes go to KSp physically, not through the preempted process's window.
            // Clearing both bits (not just bit 0) means the handler runs at full kernel
            // privilege regardless of whether it preempted user or driver.
            Cpu.Mode = 0;
            Cpu.Sp   = Cpu.Ksp;

            // Push GP regs first so iret pops them last (atomic-restore order).
            StackPush(Cpu.R0); StackPush(Cpu.R1); StackPush(Cpu.R2); StackPush(Cpu.R3);
            StackPush(Cpu.R4); StackPush(Cpu.R5); StackPush(Cpu.R6); StackPush(Cpu.R7);

            StackPush(Cpu.MLen);
            StackPush(Cpu.MBase);
            StackPush(Cpu.Fl);
            StackPush(userSp);
            StackPush(userIp);
            StackPush(marker);
        } else {
            // Kernel to kernel (also covers degenerate Mode=0b10): lightweight frame.
            StackPush(Cpu.Ip);
            StackPush(InterruptFrameMarkerKernel);
        }

        Cpu.Ip = handlerPtr;
    }

    /// <summary>
    /// Atomically return from any interrupt frame. Pops the marker, then dispatches:
    /// <c>0x00</c> kernel-mode frame (pop IP only); <c>0x01</c> user-mode frame
    /// (pop full state, restore Mode=0b01); <c>0x02</c> supervisor/driver frame
    /// (pop full state, restore Mode=0b11). Any other marker raises
    /// <see cref="SpecialInterrupts.InvalidInstruction"/>.
    /// </summary>
    public void Iret() {
        // The marker is on the kernel stack, which is what we're in
        byte marker = StackPop8();

        switch (marker) {
            case InterruptFrameMarkerKernel: {
                Cpu.Ip = StackPop();
                return;
            }

            case InterruptFrameMarkerUser:
            case InterruptFrameMarkerSupervisor: {
                // Pop in mirror order of the push.
                uint ip = StackPop();
                uint sp = StackPop();
                uint fl = StackPop();
                uint mb = StackPop();
                uint ml = StackPop();
                uint r7 = StackPop();
                uint r6 = StackPop();
                uint r5 = StackPop();
                uint r4 = StackPop();
                uint r3 = StackPop();
                uint r2 = StackPop();
                uint r1 = StackPop();
                uint r0 = StackPop();

                // Atomic-ish restore: write everything at the very end. The Mode write is
                // last so any earlier exception leaves us cleanly in kernel mode on the
                // kernel stack.
                Cpu.R0 = r0; Cpu.R1 = r1; Cpu.R2 = r2; Cpu.R3 = r3;
                Cpu.R4 = r4; Cpu.R5 = r5; Cpu.R6 = r6; Cpu.R7 = r7;
                Cpu.MBase = mb;
                Cpu.MLen  = ml;
                Cpu.Fl    = fl;
                Cpu.Sp    = sp;
                Cpu.Ip    = ip;
                // marker 0x01 -> Mode 0b01 (user); marker 0x02 -> Mode 0b11 (driver).
                Cpu.Mode  = marker == InterruptFrameMarkerSupervisor ? (byte)0b11 : (byte)0b01;
                return;
            }

            default:
                Interrupt(SpecialInterrupts.InvalidInstruction);
                return;
        }
    }
    
#endregion

#region Events

    private long CurrentPicosecondTime => Fast ? Runtime.Elapsed.Ticks * PicosecondsPerTick : TicksPassed;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void FireDueEvents() {
        while (CurrentPicosecondTime >= _nextEvent) {
            _events[^1].callback();
            _events.RemoveAt(_events.Count - 1);
            _nextEvent = _events.Count > 0 ? _events[^1].time : long.MaxValue;
        }
    }

    private void RecalculateEvents() {
        if (_events.Count == 0) {
            _nextEvent = long.MaxValue;
            return;
        }
        _events = _events.OrderByDescending(e => e.time).ToList();
        _nextEvent = _events[^1].time;
    }

    public void RunAt(long picosecondTime, Action executor) {
        _events.Add((picosecondTime, executor));
        RecalculateEvents();
    }

    public void RunIn(long picosecondTime, Action executor) {
        _events.Add((picosecondTime + CurrentPicosecondTime, executor));
        RecalculateEvents();
    }

#endregion

    public void ValidateMemoryWrite(uint address, uint size) {
        // Bounds checking is not needed here because
        // the array access will error and be caught
        // by upstream try catch.
        
        if (DebugMode && DisallowedWriteRegions.Length != 0) {
            if (ErrorOnRomWrite && address < Rom.Length) {
                throw new MemoryOutOfRange(true, address, size, "ROM writes are disallowed");
            }
            
            // disallowed write regions
            foreach ((uint start, uint length) in DisallowedWriteRegions) {
                if (address < start + length && address + size > start) {
                    throw new MemoryOutOfRange(true, address, size, "Write to disallowed memory region");
                }
            }
        }
    }
    
    public void ValidateMemoryRead(uint address, uint size) {
        // Bounds checking is not needed here because
        // the array access will error and be caught
        // by upstream try catch.
        
        if (DebugMode && DisallowedReadRegions.Length != 0) {
            // disallowed read regions
            foreach ((uint start, uint length) in DisallowedReadRegions) {
                if (address < start + length && address + size > start) {
                    throw new MemoryOutOfRange(false, address, size, "Read from disallowed memory region");
                }
            }
        }
    }
    
    /// <summary>
    /// Function-pointer dispatch table for opcodes. Indexed by opcode byte.
    /// <para/>
    /// Stored as raw <c>delegate*&lt;CatVM, void&gt;</c> rather than <c>Action&lt;CatVM&gt;</c>
    /// so the hot dispatch in <see cref="ExecuteInstruction"/> is a single indirect call
    /// with no delegate-target indirection. Cycles, names, and executors are kept in three
    /// parallel arrays so each is hot in its own cache line and the cycles array is only
    /// touched outside the loop body.
    /// </summary>
    public static readonly unsafe delegate*<CatVM, void>[] OperationExecutors;

    /// <summary>Cycle cost per opcode, indexed by opcode byte. Parallel to <see cref="OperationExecutors"/>.</summary>
    public static readonly int[] OperationCycles;

    /// <summary>Human-readable name per opcode, indexed by opcode byte. Used by the debugger.</summary>
    public static readonly string[] OperationNames;

    static CatVM() {
        // Single source of truth for the dispatch table. The legacy Operations tuple,
        // the function-pointer table, the cycles table, and the names table are all
        // derived from this list - any new opcode goes here and only here.
        // Opcode byte values are positional, so do NOT reorder existing entries.
        (string name, int cycles, Action<CatVM> executor)[] table = [
            ("MovRR",   2, MovOperation.MovRR),
            ("MovRI",   2, MovOperation.MovRI),
            ("MovRRP",  6, MovOperation.MovRRP),
            ("MovRIP",  5, MovOperation.MovRIP),
            ("MovRPR",  8, MovOperation.MovRPR),
            ("MovRPI",  7, MovOperation.MovRPI),
            ("MovIPR",  7, MovOperation.MovIPR),
            ("MovIPI",  6, MovOperation.MovIPI),
            ("SMovRRP", 6, MovOperation.SMovRRP),
            ("SMovRIP", 5, MovOperation.SMovRIP),
            ("SMovRPR", 8, MovOperation.SMovRPR),
            ("SMovRPI", 7, MovOperation.SMovRPI),
            ("SMovIPR", 7, MovOperation.SMovIPR),
            ("SMovIPI", 6, MovOperation.SMovIPI),
            ("BMovRRP", 6, MovOperation.BMovRRP),
            ("BMovRIP", 5, MovOperation.BMovRIP),
            ("BMovRPR", 8, MovOperation.BMovRPR),
            ("BMovRPI", 7, MovOperation.BMovRPI),
            ("BMovIPR", 7, MovOperation.BMovIPR),
            ("BMovIPI", 6, MovOperation.BMovIPI),
            ("AddRR",   2, AddOperation.AddRR),
            ("AddRI",   2, AddOperation.AddRI),
            ("SubRR",   2, SubOperation.SubRR),
            ("SubRI",   2, SubOperation.SubRI),
            ("MulRR",   8, MulOperation.MulRR),
            ("MulRI",   8, MulOperation.MulRI),
            ("IMulRR",  8, MulOperation.IMulRR),
            ("IMulRI",  8, MulOperation.IMulRI),
            ("DivRR",  32, DivOperation.DivRR),
            ("IDivRR", 32, DivOperation.IDivRR),
            ("IntR",   64, IntOperation.IntR),
            ("IntI",   64, IntOperation.IntI),
            ("PushR",   6, StackOperation.PushR),
            ("PushI",   6, StackOperation.PushI),
            ("Push16R", 6, StackOperation.Push16R),
            ("Push16I", 6, StackOperation.Push16I),
            ("Push8R",  6, StackOperation.Push8R),
            ("Push8I",  6, StackOperation.Push8I),
            ("PopR",    4, StackOperation.PopR),
            ("Pop16R",  4, StackOperation.Pop16R),
            ("Pop8R",   4, StackOperation.Pop8R),
            ("OrRR",    3, OrOperation.OrRR),
            ("OrRI",    3, OrOperation.OrRI),
            ("AndRR",   3, AndOperation.AndRR),
            ("AndRI",   3, AndOperation.AndRI),
            ("XorRR",   3, XorOperation.XorRR),
            ("XorRI",   3, XorOperation.XorRI),
            ("NotR",    2, NotOperation.NotR),
            ("JmpRI",   2, JmpOperation.JmpRI),
            ("CmpRR",   2, CmpOperation.CmpRR),
            ("CmpRI",   2, CmpOperation.CmpRI),
            ("CmpIR",   2, CmpOperation.CmpIR),
            ("CmpII",   2, CmpOperation.CmpII),
            ("JzRI",    3, JmpOperation.JzRI),
            ("JnzRI",   3, JmpOperation.JnzRI),
            ("JulRI",   3, JmpOperation.JulRI),
            ("JuleRI",  3, JmpOperation.JuleRI),
            ("JugRI",   3, JmpOperation.JugRI),
            ("JugeRI",  3, JmpOperation.JugeRI),
            ("JilRI",   3, JmpOperation.JilRI),
            ("JileRI",  3, JmpOperation.JileRI),
            ("JigRI",   3, JmpOperation.JigRI),
            ("JigeRI",  3, JmpOperation.JigeRI),
            ("Call",    6, StackOperation.Call),
            ("Ret",     4, StackOperation.Ret),
            ("CpyRR", 256, CpyOperation.CpyRR),
            ("CpyRI", 256, CpyOperation.CpyRI),
            ("CpyIR", 256, CpyOperation.CpyIR),
            ("CpyII", 256, CpyOperation.CpyII),
            ("Di",      2, IntOperation.Di),
            ("Ei",      2, IntOperation.Ei),
            ("InRR",   12, SerialOperation.InRR),
            ("InRI",   12, SerialOperation.InRI),
            ("OutRR",  12, SerialOperation.OutRR),
            ("OutRI",  12, SerialOperation.OutRI),
            ("OutIR",  12, SerialOperation.OutIR),
            ("OutII",  12, SerialOperation.OutII),
            ("Nop",     1, NopOperation.Nop),
            ("ShlRR",   3, ShiftOperation.ShlRR),
            ("ShlRI",   3, ShiftOperation.ShlRI),
            ("ShrRR",   3, ShiftOperation.ShrRR),
            ("ShrRI",   3, ShiftOperation.ShrRI),
            ("IRet",    8, VirtModeRetOperation.IRet),
            ("SetItR",  2, InterruptTableOperation.SetItR),
            ("SetItI",  2, InterruptTableOperation.SetItI),
            ("GetItR",  2, InterruptTableOperation.GetItR),
            ("SetKspR", 2, KspOperation.SetKspR),
            ("SetKspI", 2, KspOperation.SetKspI),
            ("GetKspR", 2, KspOperation.GetKspR),
            ("Syscall",64, IntOperation.Syscall),
        ];

        int n = table.Length;
        OperationNames  = new string[n];
        OperationCycles = new int[n];
        Operations      = new (Action<CatVM>, int)[n];
        unsafe {
            OperationExecutors = new delegate*<CatVM, void>[n];
        }

        for (int i = 0; i < n; i++) {
            (string name, int cycles, Action<CatVM> exec) = table[i];
            OperationNames[i]  = name;
            OperationCycles[i] = cycles;
            Operations[i]      = (exec, cycles);

            // Derive the function pointer from the delegate's MethodInfo. PrepareMethod
            // forces the JIT to produce the final codegen entry point now, so the pointer
            // we cache is the real method address and not a tier-0 stub.
            // All entries in the table above must be static methods - if a non-static
            // method ever slips in, the check below fires at startup.
            if (!exec.Method.IsStatic) {
                throw new InvalidOperationException(
                    $"Opcode '{name}' must be backed by a static method (got {exec.Method.DeclaringType}.{exec.Method.Name}).");
            }
            RuntimeHelpers.PrepareMethod(exec.Method.MethodHandle);
            IntPtr fp = exec.Method.MethodHandle.GetFunctionPointer();
            unsafe {
                OperationExecutors[i] = (delegate*<CatVM, void>)fp;
            }
        }
    }

    /// <summary>
    /// Legacy view of the dispatch table as <c>(Action&lt;CatVM&gt;, int)</c> tuples.
    /// Retained for any external consumer that still indexes it; the hot dispatch path
    /// uses <see cref="OperationExecutors"/> + <see cref="OperationCycles"/> instead.
    /// </summary>
    public static readonly (Action<CatVM> executor, int cycles)[] Operations;
}
