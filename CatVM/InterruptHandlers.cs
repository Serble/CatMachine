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
    
    public static void GetDisplayBufferInterrupt(CatVM vm) {
        // return pointer to display buffer in r1
        vm.Cpu.R0 = vm.DisplayBufferOffset;
    }

    public static void PrintNumInterrupt(CatVM vm) {
        Console.WriteLine(vm.Cpu.R1);
    }

    public static void GetUptimeInterrupt(CatVM vm) {
        vm.Cpu.R0 = (uint)vm.Runtime.ElapsedMilliseconds;
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
