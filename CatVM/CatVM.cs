using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CatVM.Ops;
using Raylib_cs;

namespace CatVM;

// a VM instance
public class CatVM {
    public const int DisplayWidth = 512;
    public const int DisplayHeight = 512;
    public const int DisplayBufferSize = DisplayHeight * DisplayWidth * 4;
    
    public byte[] Memory { get; set; } = null!;
    public byte[] Rom { get; set; }
    public double InstructionsPerSecond { get; set; }
    public bool Paused { get; set; }
    public bool PrintInstructionTimes { get; set; }
    public uint DisplayBufferOffset { get; set; }
    private GCHandle? _memoryHandle;
    public CatCpu Cpu;
    private readonly int _memoryBytes;

    public CatVM(int memoryBytes, double instructionsPerSecond, byte[]? rom = null) {
        _memoryBytes = memoryBytes;
        Rom = rom ?? [];
        InstructionsPerSecond = instructionsPerSecond;

        if (memoryBytes < Rom.Length + DisplayBufferSize) {
            throw new Exception($"Not enough memory for Rom and Display Buffer, needed: {Rom.Length+DisplayBufferSize}, got: {memoryBytes}");
        }
        
        Reset();
    }

    public void Reset(bool preserveMem = false) {
        Cpu = new CatCpu();
        if (!preserveMem) {
            _memoryHandle?.Free();   // Release old memory array
            Memory = new byte[_memoryBytes];
            _memoryHandle = GCHandle.Alloc(Memory, GCHandleType.Pinned);
            
            // get offset for display buffer (it will go at the end of memory)
            DisplayBufferOffset = (uint)(_memoryBytes - DisplayBufferSize);
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

    public Task RunRendering() {
        return Task.Run((Action) (() => {
            Raylib.InitWindow(DisplayWidth, DisplayHeight, "CatVM Display");

            if (!_memoryHandle.HasValue) {
                throw new Exception("Memory not initialized.");
            }

            Image image;
            unsafe {
                image = new Image {
                    Data = (_memoryHandle!.Value.AddrOfPinnedObject() + (int)DisplayBufferOffset).ToPointer(),
                    Width = DisplayWidth,
                    Height = DisplayHeight,
                    Mipmaps = 1,
                    Format = PixelFormat.UncompressedR8G8B8A8
                };
            }
            
            Texture2D texture = Raylib.LoadTextureFromImage(image);

            while (true) {
                unsafe {
                    if (Raylib.WindowShouldClose()) {
                        // close window
                        Raylib.CloseWindow();
                        Environment.Exit(0);
                    }
                
                    Raylib.UpdateTexture(texture, _memoryHandle!.Value.AddrOfPinnedObject().ToPointer());
        
                    Raylib.BeginDrawing();
                    Raylib.ClearBackground(Color.Black);

                    Raylib.DrawTexture(texture, 0, 0, Color.White);
        
                    Raylib.EndDrawing();
                }
            }
        }));
    }

    public void FastRun() {
        while (true) {
            if (Paused) {
                Thread.Yield();
                continue;
            }

            ExecuteInstruction();
        }
    }
    
    public void Run() {
        double instructionDelay = 1_000_000.0 / InstructionsPerSecond; // in microseconds
        Stopwatch stopwatch = new();
        
        while (true) {
            if (Paused) {
                Thread.Yield();
                continue;
            }

            stopwatch.Restart();
            ExecuteInstruction();
            stopwatch.Stop();

            double elapsedMicroseconds = stopwatch.Elapsed.TotalMicroseconds;
            if (PrintInstructionTimes) {
                Console.WriteLine("Elapsed: " + elapsedMicroseconds + " us");
            }
            
            double sleepTime = instructionDelay - elapsedMicroseconds;
            if (sleepTime > 0) {
                Thread.Sleep((int)(sleepTime / 1000.0));
            }
        }
    }

    public void ExecuteInstruction() {
        byte opcode = Read8();

        if (opcode > Operations.Length) {
            Interrupt(SpecialInterupts.InvalidInstruction);
            return;
        }
        
        try {
            Operations[opcode](this);
        }
        catch (DivideByZeroException) {
            Interrupt(SpecialInterupts.DivideByZero);
        }
        catch (MemoryOutOfRange e) {
            try {
                StackPush(e.Address);
                Interrupt(SpecialInterupts.PageFault);
            }
            catch (MemoryOutOfRange) {
                Interrupt(SpecialInterupts.PageFault);
            }
        }
        catch (Exception) {
            Interrupt(SpecialInterupts.InvalidInstruction);
        }
    }

    public void Interrupt(byte opcode) => HandleInterrupt(opcode);
    public void Interrupt(SpecialInterupts opcode) => HandleInterrupt((byte)opcode);
    public void HandleInterrupt(byte opcode) {
        // System functions
        switch (opcode) {
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
        }
        
        // User defined interrupt (or default)
        if (Cpu.It == uint.MaxValue) {
            // default
            InterruptHandlers.DefaultHandler(this, opcode);
            return;
        }
        
        // Find the handler
        byte entryCount = Read8(Cpu.It);

        uint entryPtr = Cpu.It + 1;
        for (int i = 0; i < entryCount; i++) {
            byte code = Read8(entryPtr);
            uint handlerPtr = ReadWord(entryPtr + 1);
            if (code == opcode) {
                // found
                // push state
                StackPush(Cpu.Ip);
                Cpu.Ip = handlerPtr;
                return;  // now executing the handler
            }
        }
        
        // not found, default
        InterruptHandlers.DefaultHandler(this, opcode);
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
    
    public static readonly Action<CatVM>[] Operations = [
        MovOperation.MovRR,
        MovOperation.MovRI,
        MovOperation.MovRRP,
        MovOperation.MovRIP,
        MovOperation.MovRPR,
        MovOperation.MovRPI,
        MovOperation.MovIPR,
        MovOperation.MovIPI,
        MovOperation.BMovIPR,
        MovOperation.BMovRPR,
        MovOperation.BMovRIP,
        MovOperation.BMovRRP,
        MovOperation.SMovIPR,
        MovOperation.SMovRPR,
        MovOperation.SMovRIP,
        MovOperation.SMovRRP,
        AddOperation.AddRR,
        AddOperation.AddRI,
        SubOperation.SubRR,
        SubOperation.SubRI,
        MulOperation.MulRR,
        MulOperation.MulRI,
        MulOperation.IMulRR,
        MulOperation.IMulRI,
        DivOperation.DivRR,
        DivOperation.IDivRR,
        IntOperation.IntR,
        IntOperation.IntI,
        StackOperation.PushR,
        StackOperation.PushI,
        StackOperation.Push16R,
        StackOperation.Push16I,
        StackOperation.Push8R,
        StackOperation.Push8I,
        StackOperation.PopR,
        StackOperation.Pop16R,
        StackOperation.Pop8R,
        OrOperation.OrRR,
        OrOperation.OrRI,
        AndOperation.AndRR,
        AndOperation.AndRI,
        XorOperation.XorRR,
        XorOperation.XorRI,
        NotOperation.NotR,
        JmpOperation.JmpRI,
        CmpOperation.CmpRR,
        CmpOperation.CmpRI,
        CmpOperation.CmpIR,
        CmpOperation.CmpII,
        JmpOperation.JzRI,
        JmpOperation.JnzRI,
        JmpOperation.JbRI,
        JmpOperation.JbeRI,
        JmpOperation.JaRI,
        JmpOperation.JaeRI,
        JmpOperation.JlRI,
        JmpOperation.JleRI,
        JmpOperation.JgRI,
        JmpOperation.JgeRI,
        StackOperation.Call,
        StackOperation.Ret,
        CpyOperation.CpyRR,
        CpyOperation.CpyRI,
        CpyOperation.CpyIR,
        CpyOperation.CpyII
    ];
}
