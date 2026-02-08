namespace CatVM;

public static class InterruptHandlers {

    // pointer to c style string in r1
    public static void PrintInterrupt(CatVM vm) {
        string message = vm.ReadString(vm.Cpu.R1);
        Console.Write(message);
    }
    
    public static void HaltInterrupt(CatVM vm) {
        vm.Paused = true;
    }
    
    public static void ShutdownInterrupt(CatVM vm) {
        Console.WriteLine("CatVM is shutting down...");
        Environment.Exit(0);
    }
    
    public static void ResetInterrupt(CatVM vm) {
        vm.Reset();
    }

    public static void PrintNumInterrupt(CatVM vm) {
        Console.WriteLine($"{vm.Cpu.R1} 0x{vm.Cpu.R1:x8}");
    }

    public static void GetUptimeInterrupt(CatVM vm) {
        vm.Cpu.R0 = vm.Fast ? (uint)vm.Runtime.ElapsedMilliseconds : (uint)(vm.TicksPassed / CatVM.PicosecondsPerMillisecond);
    }

    public static void UpdateDisplayInterrupt(CatVM vm) {
        vm.UpdateDisplay();
    }

    public static void ChangeDisplayModeInterrupt(CatVM vm) {
        uint displayMode = vm.Cpu.R1;
        if (!Enum.IsDefined(typeof(DisplayMode), (int)displayMode)) {
            vm.Cpu.R0 = 1;
            return;
        }

        DisplayMode oldMode = vm.DisplayMode;
        uint oldOffset = vm.DisplayBufferAddress;
        vm.DisplayMode = (DisplayMode)displayMode;
        vm.DisplayBufferAddress = vm.Cpu.R2;

        if (vm.DisplayBufferAddress + vm.DisplayBufferSize > vm.Memory.Length) {
            vm.Cpu.R0 = 2;
            vm.DisplayMode = oldMode;
            vm.DisplayBufferAddress = oldOffset;
            return;
        }
        
        vm.Cpu.R0 = 0;
    }

    public static void DefaultHandler(CatVM vm, byte opcode) {
        if (opcode >= 0x10) return;  // ignore non errors
        
        // error
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine($"Error Interrupt: Code {opcode}");
        Console.WriteLine(vm.Cpu.Dump());
        Console.WriteLine("================================");
        Console.WriteLine("Halting...");
        vm.Paused = true;
    }
}
