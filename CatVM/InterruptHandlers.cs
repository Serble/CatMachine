namespace CatVM;

public static class InterruptHandlers {

    // pointer to c style string in r1
    public static void PrintInterrupt(CatVm vm) {
        string message = vm.ReadString(vm.Cpu.R1);
        Console.Write(message);
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
