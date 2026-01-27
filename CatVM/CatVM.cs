using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using CatVM.Ops;

namespace CatVM;

// a VM instance
public class CatVM {
    public const bool BenchmarkMode = true;
    public const bool DebugMode = false;
    public const int DisplayWidth = 512;
    public const int DisplayHeight = 512;
    public const int DisplayBufferSize = DisplayHeight * DisplayWidth * 4;
    
    public const long PicosecondsPerSecond = 1000000000000L;
    public const long PicosecondsPerTick = 100000L;
    public const long PicosecondsPerMillisecond = 1000000000L;
    
    public byte[] Memory { get; set; } = null!;
    public byte[] Rom { get; set; }
    public bool InterruptsEnabled { get; set; } = true;
    public long PicosecondsPerCycle { get; set; }
    public bool ErrorOnRomWrite { get; set; }
    public bool EnableTestingInterrupts { get; set; }
    public bool DumpErrors { get; set; }
    public uint DisplayBufferOffset { get; set; }
    public (uint start, uint length)[] DisallowedWriteRegions { get; set; } = [];
    public (uint start, uint length)[] DisallowedReadRegions { get; set; } = [];
    public GCHandle? MemoryHandle { get; private set; }
    public Queue<byte> InterruptQueue { get; } = [];
    public long TicksPassed { get; private set; } // This isn't real time this is virtual time, 1 tick = 1 picosecond
    private DateTime lastSlowWarning = DateTime.MinValue;   
    public Stopwatch Runtime { get; } = new();
    public bool Fast { get; init; }
    public event Action? UpdateDisplayEvent;  // Event for when the program requests the display to update
    public CatCpuState Cpu;
    
    private readonly int _memoryBytes;
    
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

    public Dictionary<uint, (Func<CatVM, uint> input, Action<CatVM, uint> output)> SerialDevices { get; } = [];
    
    public CatVM(int memoryBytes, uint cyclesPerSecond, byte[]? rom = null) {
        _memoryBytes = memoryBytes;
        Rom = rom ?? [];
        PicosecondsPerCycle = PicosecondsPerSecond / cyclesPerSecond;

        if (memoryBytes < Rom.Length + DisplayBufferSize) {
            throw new Exception($"Not enough memory for Rom and Display Buffer, needed: {Rom.Length+DisplayBufferSize}, got: {memoryBytes}");
        }
        
        Reset();
    }

    public void Reset(bool preserveMem = false) {
        Cpu = new CatCpuState();
        if (!preserveMem) {
            MemoryHandle?.Free();   // Release old memory array
            MemoryHandle = null;
            Memory = new byte[_memoryBytes];
            MemoryHandle = GCHandle.Alloc(Memory, GCHandleType.Pinned);
        }
        
        // get offset for display buffer (it will go at the end of memory)
        DisplayBufferOffset = (uint)(_memoryBytes - DisplayBufferSize);
        Cpu.Sp = DisplayBufferOffset;  // end of regular memory (non display buffer)
        
        if (Rom.Length > 0) {
            LoadData(Rom);
        }
    }
    
    public void LoadData(byte[] data, uint offset = 0) {
        if (offset + data.Length > Memory.Length) {
            throw new Exception("ROM exceeds memory bounds.");
        }
        Array.Copy(data, 0, Memory, offset, data.Length);
        Cpu.Ip = offset;
    }
    
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
    
    public void Run(CancellationToken? cancellationToken = null) {
        Runtime.Restart();
        
        int instructionsExecuted = 0;
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
            try {
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
                Console.WriteLine(trace.ToString());
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
            catch (Exception e) {
                DumpError(e);
                Interrupt(SpecialInterupts.InvalidInstruction);
            }
        }
    }

    public void DumpError(Exception e) {
        if (DumpErrors) {
            Console.WriteLine(e);
        }
    }
    
    public void ExecuteInstruction(bool fast = false) {
        // Don't use TryDequeue for performance reasons
        if (InterruptsEnabled && InterruptQueue.Count != 0) {
            HandleInterrupt(InterruptQueue.Dequeue());
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
        } else if (sleepNeeded < -100 * PicosecondsPerMillisecond && DateTime.Now - lastSlowWarning > TimeSpan.FromMilliseconds(1000)) {
            Console.WriteLine($"VM is running {sleepNeeded / -PicosecondsPerMillisecond}ms behind!");
            lastSlowWarning = DateTime.Now;
        }
    }

    public void Interrupt(SpecialInterupts id) => Interrupt((byte)id);
    public void Interrupt(byte id) {
        InterruptQueue.Enqueue(id);
    }
    
    public void HandleInterrupt(byte id) {
        // System functions
        switch (id) {
            case 0x80: {
                // print
                InterruptHandlers.PrintInterrupt(this);
                return;
            }

            case 0x81: {
                // halt
                InterruptHandlers.HaltInterrupt(this);
                return;
            }
            
            case 0x82: {
                // shutdown
                InterruptHandlers.ShutdownInterrupt(this);
                return;
            }
            
            case 0x83: {
                // reset
                InterruptHandlers.ResetInterrupt(this);
                return;
            }
            
            case 0x84: {
                // get display buffer
                InterruptHandlers.GetDisplayBufferInterrupt(this);
                return;
            }
            
            case 0x85: {
                // get uptime
                InterruptHandlers.GetUptimeInterrupt(this);
                return;
            }

            case 0x86: {
                // update display
                InterruptHandlers.UpdateDisplayInterrupt(this);
                return;
            }
            
            case 0x90 when EnableTestingInterrupts: {
                // print number
                InterruptHandlers.PrintNumInterrupt(this);
                return;
            }
        }
        
        // User defined interrupt (or default)
        if (Cpu.It == uint.MaxValue) {
            // default
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
        (JmpOperation.JbRI, 3),
        (JmpOperation.JbeRI, 3),
        (JmpOperation.JaRI, 3),
        (JmpOperation.JaeRI, 3),
        (JmpOperation.JlRI, 3),
        (JmpOperation.JleRI, 3),
        (JmpOperation.JgRI, 3),
        (JmpOperation.JgeRI, 3),
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
        (NopOperation.Nop, 1)
    ];
}
