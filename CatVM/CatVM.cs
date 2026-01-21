using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CatVM.Ops;

namespace CatVM;

// a VM instance
public class CatVM {
    public const int DisplayWidth = 512;
    public const int DisplayHeight = 512;
    public const int DisplayBufferSize = DisplayHeight * DisplayWidth * 4;
    
    public byte[] Memory { get; set; } = null!;
    public byte[] Rom { get; set; }
    public bool InterruptsEnabled { get; set; } = true;
    public double CyclesPerSecond { get; set; }
    public bool PrintInstructionTimes { get; set; }
    public bool EnableTestingInterrupts { get; set; }
    public bool DumpErrors { get; set; }
    public uint DisplayBufferOffset { get; set; }
    public GCHandle? MemoryHandle { get; private set; }
    public Queue<byte> InterruptQueue { get; } = [];
    public Stopwatch Runtime { get; } = new();
    public event Action UpdateDisplayEvent = null!;  // Event for when the program requests the display to update
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

    public double SecondsPerCycle => 1 / CyclesPerSecond;

    public Dictionary<uint, (Func<CatVM, uint> input, Action<CatVM, uint> output)> SerialDevices { get; } = [];
    
    public CatVM(int memoryBytes, double cyclesPerSecond, byte[]? rom = null) {
        _memoryBytes = memoryBytes;
        Rom = rom ?? [];
        CyclesPerSecond = cyclesPerSecond;

        if (memoryBytes < Rom.Length + DisplayBufferSize) {
            throw new Exception($"Not enough memory for Rom and Display Buffer, needed: {Rom.Length+DisplayBufferSize}, got: {memoryBytes}");
        }
        
        Reset();
    }

    public void Reset(bool preserveMem = false) {
        Cpu = new CatCpuState();
        if (!preserveMem) {
            MemoryHandle?.Free();   // Release old memory array
            Memory = new byte[_memoryBytes];
            MemoryHandle = GCHandle.Alloc(Memory, GCHandleType.Pinned);
            
            // get offset for display buffer (it will go at the end of memory)
            DisplayBufferOffset = (uint)(_memoryBytes - DisplayBufferSize);
            Cpu.Sp = DisplayBufferOffset;  // end of regular memory (non display buffer)
        }
        
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
    
    public byte Read8() {
        if (Cpu.Ip >= Memory.Length) {
            throw new MemoryOutOfRange(Cpu.Ip);
        }
        return Memory[Cpu.Ip++];
    }
    
    public byte Read8(uint ptr) {
        if (ptr >= Memory.Length) {
            throw new MemoryOutOfRange(ptr);
        }
        return Memory[ptr];
    }
    
    public ushort Read16() {
        if (Cpu.Ip + 2 > Memory.Length) {
            throw new MemoryOutOfRange(Cpu.Ip);
        }
        ushort value = BitConverter.ToUInt16(Memory, (int)Cpu.Ip);
        Cpu.Ip += 2;
        return value;
    }
    
    public ushort Read16(uint ptr) {
        if (ptr + 2 > Memory.Length) {
            throw new MemoryOutOfRange(ptr);
        }
        return BitConverter.ToUInt16(Memory, (int)ptr);
    }
    
    public uint ReadWord() {
        if (Cpu.Ip + 4 > Memory.Length) {
            throw new MemoryOutOfRange(Cpu.Ip);
        }
        uint value = BitConverter.ToUInt32(Memory, (int)Cpu.Ip);
        Cpu.Ip += 4;
        return value;
    }
    
    public uint ReadWord(uint ptr) {
        if (ptr + 4 > Memory.Length) {
            throw new MemoryOutOfRange(ptr);
        }
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
    
    public void Run(bool fast = false) {
        Runtime.Restart();
        
        while (true) {
            if (Paused) {
                Thread.Yield();
                continue;
            }
            
            ExecuteInstruction(fast);
        }
    }

    private void DumpError(Exception e) {
        if (DumpErrors) {
            Console.WriteLine(e);
        }
    }

    public void ExecuteInstruction(bool fast = false) {
        if (InterruptsEnabled && InterruptQueue.TryDequeue(out byte waitingInterrupt)) {
            HandleInterrupt(waitingInterrupt);
        }

        Stopwatch sw = Stopwatch.StartNew();
        int instructionCycles = 0;
        try {
            byte opcode = Read8();

            if (opcode > Operations.Length) {
                Interrupt(SpecialInterupts.InvalidInstruction);
                return;
            }

            (Action<CatVM> executor, int cycles) instruction = Operations[opcode];
            instructionCycles = instruction.cycles;
            instruction.executor(this);
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
        catch (Exception e) {
            DumpError(e);
            Interrupt(SpecialInterupts.InvalidInstruction);
        }

        if (PrintInstructionTimes) {
            Console.WriteLine("Actual OP execution took: " + sw.Elapsed.Microseconds + " us");
        }
        
        // wait the required time
        TimeSpan instructionPenalty = TimeSpan.FromSeconds(SecondsPerCycle * instructionCycles) - sw.Elapsed;
        if (!fast && instructionPenalty > TimeSpan.Zero) {
            Thread.Sleep(instructionPenalty);
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
                break;
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
        if (Cpu.Sp < 4) {
            throw new MemoryOutOfRange(Cpu.Sp - 4);
        }
        Cpu.Sp -= 4;
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, Memory, (int)Cpu.Sp, 4);
    }
    
    public void StackPush(byte value) {
        if (Cpu.Sp < 1) {
            throw new MemoryOutOfRange(Cpu.Sp - 1);
        }
        Cpu.Sp -= 1;
        Memory[Cpu.Sp] = value;
    }
    
    public void StackPush(ushort value) {
        if (Cpu.Sp < 2) {
            throw new MemoryOutOfRange(Cpu.Sp - 2);
        }
        Cpu.Sp -= 2;
        byte[] bytes = BitConverter.GetBytes(value);
        Array.Copy(bytes, 0, Memory, (int)Cpu.Sp, 2);
    }
    
    public uint StackPop() {
        uint value = BitConverter.ToUInt32(Memory, (int)Cpu.Sp);
        Cpu.Sp += 4;
        return value;
    }
    
    public byte StackPop8() {
        byte value = Memory[Cpu.Sp];
        Cpu.Sp += 1;
        return value;
    }
    
    public ushort StackPop16() {
        ushort value = BitConverter.ToUInt16(Memory, (int)Cpu.Sp);
        Cpu.Sp += 2;
        return value;
    }

    public void UpdateDisplay() {
        UpdateDisplayEvent();
    }

    public void SaveState(Stream stream) {
        stream.Write(Memory);
        Cpu.SaveState(stream);
    }
    
    public void LoadState(Stream stream) {
        _ = stream.Read(Memory, 0, Memory.Length);
        Cpu = CatCpuState.LoadState(stream);
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
