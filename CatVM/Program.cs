
string romPath = args.Length > 0 ? args[0] : throw new ArgumentException("Please provide a path to a CatVM ROM file.");

if (!File.Exists(romPath)) {
    throw new FileNotFoundException("ROM file not found.", romPath);
}

// flags
bool fastRun = false;
int ops = 100_000;  // ops per seconds
int memorySize = 1024 * 1024 * 16; // 16mb
bool enableTimings = false;

for (int i = 1; i < args.Length; i++) {
    switch (args[i]) {
        case "--fast":
            fastRun = true;
            break;
        
        case "--ops":
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out int parsedOps)) {
                ops = parsedOps;
                i++;
            } else {
                Console.WriteLine("Invalid or missing value for --ops flag.");
            }
            break;
        
        case "--mem":
            if (i + 1 < args.Length && int.TryParse(args[i + 1], out int parsedMem)) {
                memorySize = parsedMem;
                i++;
            } else {
                Console.WriteLine("Invalid or missing value for --mem flag.");
            }
            break;
        
        case "--timings":
            enableTimings = true;
            break;
        
        default:
            Console.WriteLine($"Unknown flag: {args[i]}");
            break;
    }
}

CatVM.CatVM vm = new(memorySize, ops, File.ReadAllBytes(romPath)) {
    PrintInstructionTimes = enableTimings
};

_ = vm.RunRendering();

if (fastRun) {
    vm.FastRun();
}
else {
    vm.Run();
}

// should never exit
Console.WriteLine("Exited?");
