namespace CatVM;

public static class InterruptHandlers {

    // pointer to c style string in r1
    public static void PrintInterrupt(CatVM vm) {
        uint strPtr = vm.Cpu.Get(1);
        string message = vm.ReadString(strPtr);
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
