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
    /// The memory of the VM. Be careful when modifying this directly.
    /// </summary>
    public byte[] Memory { get; set; } = null!;
    
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
    /// The address of the display buffer in the VM's memory.
    /// <remarks>This should only really be set by user program.</remarks>
    /// </summary>
    public uint DisplayBufferAddress { get; set; }
    
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
    /// <see cref="HardwareInterrupt(byte)"/> or <see cref="HardwareInterrupt(SpecialInterupts)"/>.
    /// </summary>
    public Queue<byte> HardwareInterruptQueue { get; } = [];
    
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
    /// Event for when the program requests the display to update.
    /// <p/>
    /// Display hardware should subscribe to this event and update the display when it is invoked.
    /// </summary>
    public event Action? UpdateDisplayEvent;
    
    /// <summary>
    /// Event for when the program changes the display mode.
    /// The display mode can be read from the <see cref="DisplayMode"/> property.
    /// <p/>
    /// Display hardware should subscribe to this event and update the display when it is invoked.
    /// </summary>
    public event Action? DisplayModeUpdated;
    
    /// <summary>
    /// The virtual CPU state of the VM, including registers and flags.
    /// This is used by instructions to read and modify the CPU state.
    /// <p/>
    /// Be careful when modifying this directly. It is not recommended.
    /// </summary>
    public CatCpuState Cpu;

    /// <summary>
    /// The width of the current display mode.
    /// </summary>
    public int DisplayWidth { get; private set; }
    
    /// <summary>
    /// The height of the current display mode.
    /// </summary>
    public int DisplayHeight { get; private set; }
    
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
    
    public DisplayMode DisplayMode { get;
        set {
            field = value;

            if (value == DisplayMode.DummyDisplay) {
                DisplayWidth = 0;
                DisplayHeight = 0;
            }
            else {
                switch ((int)value & 0xf) {
                    case 0:
                        DisplayWidth = 512;
                        DisplayHeight = 512;
                        break;
                
                    case 1:
                        DisplayWidth = 512;
                        DisplayHeight = 384;
                        break;
                }
            }
            
            DisplayModeUpdated?.Invoke();
        }
    } = DisplayMode.DummyDisplay;
    
    /// <summary>
    /// The size of the display buffer in bytes for the current display mode.
    /// Refer to <see cref="DisplayBufferAddress"/> for where the display buffer is located in memory.
    /// </summary>
    public int DisplayBufferSize {
        get {
            if (DisplayMode == DisplayMode.DummyDisplay) {
                return 0;
            }
            
            return ((int)DisplayMode & 0xf) switch {
                0 => DisplayWidth * DisplayHeight * 4,
                1 => 34_868,
                _ => 0
            };
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
        uint port = 16;
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

#region Memory Read/Write Methods
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Read8() {
        ValidateMemoryRead(Cpu.Ip, 1);
        return Memory[Cpu.Ip++];
    }
    
    public byte Read8(uint ptr) {
        ValidateMemoryRead(ptr, 1);
        return Memory[ptr];
    }
    
    public ushort Read16() {
        ValidateMemoryRead(Cpu.Ip, 2);
        ushort value = BitConverter.ToUInt16(Memory, (int)Cpu.Ip);
        Cpu.Ip += 2;
        return value;
    }
    
    public ushort Read16(uint ptr) {
        ValidateMemoryRead(ptr, 2);
        return BitConverter.ToUInt16(Memory, (int)ptr);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadWord() {
        ValidateMemoryRead(Cpu.Ip, 4);
        uint value = BitConverter.ToUInt32(Memory, (int)Cpu.Ip);
        Cpu.Ip += 4;
        return value;
    }
    
    public uint ReadWord(uint ptr) {
        ValidateMemoryRead(ptr, 4);
        return BitConverter.ToUInt32(Memory, (int)ptr);
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
    
    public void StackPush(uint value) {
        Cpu.Sp -= 4;
        ValidateMemoryWrite(Cpu.Sp, 4);
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, Memory, (int)Cpu.Sp, 4);
    }
    
    public void StackPush(byte value) {
        Cpu.Sp -= 1;
        ValidateMemoryWrite(Cpu.Sp, 1);
        Memory[Cpu.Sp] = value;
    }
    
    public void StackPush(ushort value) {
        Cpu.Sp -= 2;
        ValidateMemoryWrite(Cpu.Sp, 2);
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, Memory, (int)Cpu.Sp, 2);
    }
    
    public uint StackPop() {
        ValidateMemoryRead(Cpu.Sp, 4);
        uint value = BitConverter.ToUInt32(Memory, (int)Cpu.Sp);
        Cpu.Sp += 4;
        return value;
    }
    
    public byte StackPop8() {
        ValidateMemoryRead(Cpu.Sp, 1);
        byte value = Memory[Cpu.Sp];
        Cpu.Sp += 1;
        return value;
    }
    
    public ushort StackPop16() {
        ValidateMemoryRead(Cpu.Sp, 2);
        ushort value = BitConverter.ToUInt16(Memory, (int)Cpu.Sp);
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
            Interrupt(SpecialInterupts.DivideByZero);
        }
        catch (MemoryOutOfRange e) {
            DumpError(e);
            try {
                StackPush(e.Address);
                Interrupt(SpecialInterupts.PageFault);
            }
            catch (MemoryOutOfRange ex) {
                DumpError(ex);
                Interrupt(SpecialInterupts.PageFault);
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
                Interrupt(SpecialInterupts.InvalidInstruction);
            }
            else {
                // some other index out of range (we'll assume with memory)
                Interrupt(SpecialInterupts.PageFault);
            }
        }
        catch (ArgumentException e) {
            DumpError(e);
            Interrupt(SpecialInterupts.PageFault);
        }
        catch (Exception e) {
            DumpError(e);
            Interrupt(SpecialInterupts.InvalidInstruction);
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
    public void ExecuteInstruction(bool fast = false) {
        if (InterruptsEnabled && HardwareInterruptQueue.Count != 0) {
            HandleInterrupt(HardwareInterruptQueue.Dequeue());
        }
        
        byte opcode = Read8();
        
        // don't bounds check opcode because the array lookup
        // will do that for us and throw an IndexOutOfRangeException
        (Action<CatVM> executor, int cycles) instruction = Operations[opcode];
        instruction.executor(this);
        TicksPassed += instruction.cycles * PicosecondsPerCycle;
        
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
    public void Interrupt(SpecialInterupts id) => Interrupt((byte)id);
    
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
    public void HardwareInterrupt(SpecialInterupts id) => HardwareInterrupt((byte)id);
    
    /// <summary>
    /// Send a hardware interrupt to the application.
    /// This will be added to the <see cref="HardwareInterruptQueue"/> and
    /// handled when possible.
    /// </summary>
    /// <param name="id">The interrupt code.</param>
    public void HardwareInterrupt(byte id) {
        HardwareInterruptQueue.Enqueue(id);
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

            case 0x86: {  // update display
                InterruptHandlers.UpdateDisplayInterrupt(this);
                return;
            }

            case 0x87: {  // change display mode
                InterruptHandlers.ChangeDisplayModeInterrupt(this);
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
        
        // Find the handler
        byte entryCount = Read8(Cpu.It);

        uint entryPtr = Cpu.It + 1;
        for (int i = 0; i < entryCount; i++) {
            byte code = Read8(entryPtr);
            uint handlerPtr = ReadWord(entryPtr + 1);
            if (code == id) {
                // found
                // push state
                StackPush(Cpu.Ip);
                Cpu.Ip = handlerPtr;
                return;  // now executing the handler
            }

            entryPtr += 5;
        }
        
        // not found, default
        InterruptHandlers.DefaultHandler(this, id);
    }
    
#endregion

    /// <summary>
    /// Tell any display hardware to update the display.
    /// This is usually called by an interrupt.
    /// </summary>
    public void UpdateDisplay() {
        UpdateDisplayEvent?.Invoke();
    }

    public void SaveState(Stream stream) {
        stream.Write(Memory);
        Cpu.SaveState(stream);
    }
    
    public void LoadState(Stream stream) {
        _ = stream.Read(Memory, 0, Memory.Length);
        Cpu = CatCpuState.LoadState(stream);
    }

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
    /// List of all operations supported by the VM, along with their cycle counts.
    /// </summary>
    public static readonly (Action<CatVM> executor, int cycles)[] Operations = [
        (MovOperation.MovRR, 2),
        (MovOperation.MovRI, 2),
        (MovOperation.MovRRP, 6),
        (MovOperation.MovRIP, 5),
        (MovOperation.MovRPR, 8),
        (MovOperation.MovRPI, 7),
        (MovOperation.MovIPR, 7),
        (MovOperation.MovIPI, 6),
        (MovOperation.SMovRRP, 6),
        (MovOperation.SMovRIP, 5),
        (MovOperation.SMovRPR, 8),
        (MovOperation.SMovRPI, 7),
        (MovOperation.SMovIPR, 7),
        (MovOperation.SMovIPI, 6),
        (MovOperation.BMovRRP, 6),
        (MovOperation.BMovRIP, 5),
        (MovOperation.BMovRPR, 8),
        (MovOperation.BMovRPI, 7),
        (MovOperation.BMovIPR, 7),
        (MovOperation.BMovIPI, 6),
        (AddOperation.AddRR, 2),
        (AddOperation.AddRI, 2),
        (SubOperation.SubRR, 2),
        (SubOperation.SubRI, 2),
        (MulOperation.MulRR, 8),
        (MulOperation.MulRI, 8),
        (MulOperation.IMulRR, 8),
        (MulOperation.IMulRI, 8),
        (DivOperation.DivRR, 32),
        (DivOperation.IDivRR, 32),
        (IntOperation.IntR, 64),
        (IntOperation.IntI, 64),
        (StackOperation.PushR, 6),
        (StackOperation.PushI, 6),
        (StackOperation.Push16R, 6),
        (StackOperation.Push16I, 6),
        (StackOperation.Push8R, 6),
        (StackOperation.Push8I, 6),
        (StackOperation.PopR, 4),
        (StackOperation.Pop16R, 4),
        (StackOperation.Pop8R, 4),
        (OrOperation.OrRR, 3),
        (OrOperation.OrRI, 3),
        (AndOperation.AndRR, 3),
        (AndOperation.AndRI, 3),
        (XorOperation.XorRR, 3),
        (XorOperation.XorRI, 3),
        (NotOperation.NotR, 2),
        (JmpOperation.JmpRI, 2),
        (CmpOperation.CmpRR, 2),
        (CmpOperation.CmpRI, 2),
        (CmpOperation.CmpIR, 2),
        (CmpOperation.CmpII, 2),
        (JmpOperation.JzRI, 3),
        (JmpOperation.JnzRI, 3),
        (JmpOperation.JulRI, 3),
        (JmpOperation.JuleRI, 3),
        (JmpOperation.JugRI, 3),
        (JmpOperation.JugeRI, 3),
        (JmpOperation.JilRI, 3),
        (JmpOperation.JileRI, 3),
        (JmpOperation.JigRI, 3),
        (JmpOperation.JigeRI, 3),
        (StackOperation.Call, 6),
        (StackOperation.Ret, 4),
        (CpyOperation.CpyRR, 256),
        (CpyOperation.CpyRI, 256),
        (CpyOperation.CpyIR, 256),
        (CpyOperation.CpyII, 256),
        (IntOperation.Di, 2),
        (IntOperation.Ei, 2),
        (SerialOperation.InRR, 12),
        (SerialOperation.InRI, 12),
        (SerialOperation.OutRR, 12),
        (SerialOperation.OutRI, 12),
        (SerialOperation.OutIR, 12),
        (SerialOperation.OutII, 12),
        (NopOperation.Nop, 1),
        (ShiftOperation.ShlRR, 3),
        (ShiftOperation.ShlRI, 3),
        (ShiftOperation.ShrRR, 3),
        (ShiftOperation.ShrRI, 3),
    ];
}
