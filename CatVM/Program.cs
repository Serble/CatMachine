
using CatVM.Display;

string romPath = args.Length > 0 ? args[0] : throw new ArgumentException("Please provide a path to a CatVM ROM file.");

if (!File.Exists(romPath)) {
    throw new FileNotFoundException("ROM file not found.", romPath);
}

// flags
bool fastRun = false;
uint ops = 100_000;  // ops per seconds
int memorySize = 1024 * 1024 * 16; // 16mb
bool enableTimings = false;
bool enableTestInts = false;
bool dumpErrors = false;
bool errorOnRomWrite = false;
List<(uint addr, uint length)> disallowedWrite = [];
List<(uint addr, uint length)> disallowedRead = [];

IRenderer renderer = new DummyRendering();

for (int i = 1; i < args.Length; i++) {
    switch (args[i]) {
        case "--fast":
            fastRun = true;
            break;
        
        case "--ops":
            if (i + 1 < args.Length && uint.TryParse(args[i + 1], out ops)) {
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
        
        case "--renderer":
            string rendererType = "raylib";
            if (i + 1 < args.Length) {
                rendererType = args[i + 1];
                i++;
            }

            renderer = rendererType switch {
                "raylib" => new RaylibRendering(),
                "dummy" => new DummyRendering(),
                _ => throw new ArgumentException($"Unknown rendering type: {rendererType}")
            };
            break;
        
        case "--test-ints":
            enableTestInts = true;
            break;
        
        case "--dump-errors":
            dumpErrors = true;
            break;
        
        case "--protect-rom":
            errorOnRomWrite = true;
            break;
        
        case "--disallow-write":
            if (i + 2 < args.Length &&
                uint.TryParse(args[i + 1], out uint writeAddr) &&
                uint.TryParse(args[i + 2], out uint writeLength)) {
                disallowedWrite.Add((writeAddr, writeLength));
                i += 2;
            } else {
                Console.WriteLine("Invalid or missing values for --disallow-write flag.");
            }
            break;
        
        case "--disallow-read":
            if (i + 2 < args.Length &&
                uint.TryParse(args[i + 1], out uint readAddr) &&
                uint.TryParse(args[i + 2], out uint readLength)) {
                disallowedRead.Add((readAddr, readLength));
                i += 2;
            } else {
                Console.WriteLine("Invalid or missing values for --disallow-read flag.");
            }
            break;
        
        default:
            Console.WriteLine($"Unknown flag: {args[i]}");
            break;
    }
}

CatVM.CatVM vm = new(memorySize, ops, File.ReadAllBytes(romPath)) {
    PrintInstructionTimes = enableTimings,
    EnableTestingInterrupts = enableTestInts,
    DumpErrors = dumpErrors,
    ErrorOnRomWrite = errorOnRomWrite,
    DisallowedReadRegions = disallowedRead.ToArray(),
    DisallowedWriteRegions = disallowedWrite.ToArray(),
    Fast = fastRun
};

renderer.Initialize(vm);
_ = renderer.Start(vm);

CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, _) => cts.Cancel();

vm.Run(cts.Token);
Console.WriteLine("Goodbye!");
