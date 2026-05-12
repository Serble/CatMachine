namespace CatVM;

public static class InterruptHandlers {

    // pointer to c style string in r1
    public static void PrintInterrupt(CatVm vm) {
        string message = vm.ReadString(vm.Cpu.R1);
        Console.Write(message);
    }
    
    public static void HaltInterrupt(CatVm vm) {
        vm.Paused = true;
    }
    
    public static void ShutdownInterrupt(CatVm vm) {
        Console.WriteLine("CatVM is shutting down...");
        Environment.Exit(0);
    }
    
    public static void ResetInterrupt(CatVm vm) {
        vm.Reset();
    }

    public static void PrintNumInterrupt(CatVm vm) {
        Console.WriteLine($"{vm.Cpu.R1} 0x{vm.Cpu.R1:x8}");
    }

    public static void DefaultHandler(CatVm vm, byte opcode) {
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
