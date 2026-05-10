using System.Net;
using CatVM.Debugging;
using CatVM.Extensions;
using CatVM.Extensions.Renderer;
using CatVM.Serial;

string romPath = args.Length > 0 ? args[0] : throw new ArgumentException("Please provide a path to a CatVM ROM file.");

if (!File.Exists(romPath)) {
    throw new FileNotFoundException("ROM file not found.", romPath);
}

// flags
bool fastRun = false;
uint ops = 100_000;  // ops per seconds
int memorySize = 1024 * 1024 * 16; // 16mb
bool enableTestInts = false;
bool dumpErrors = false;
bool errorOnRomWrite = false;
bool useDebugger = false;
bool raylibPpu = false;
List<(uint addr, uint length)> disallowedWrite = [];
List<(uint addr, uint length)> disallowedRead = [];
List<ISerialDevice> genericSerialDevices = [];
Dictionary<uint, ISerialDevice> serialDevices = [];
Dictionary<uint, Func<CatVM.CatVM, ISerialDevice>> serialDeviceFactories = [];

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
        
        case "--raylib-ppu":
            raylibPpu = true;
            break;
        
        case "--test-ints":
            enableTestInts = true;
            break;
        
        case "--dump-errors":
            dumpErrors = true;
            break;
        
        case "--debug":
            useDebugger = true;
            break;
        
        case "--protect-rom":
            errorOnRomWrite = true;
            break;
        
        case "--disallow-write":
            if (!CatVM.CatVM.DebugMode) {
                Console.WriteLine("--disallow-write flag requires the VM to be built in debug mode.");
                return 1;
            }
            
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
            if (!CatVM.CatVM.DebugMode) {
                Console.WriteLine("--disallow-read flag requires the VM to be built in debug mode.");
                return 1;
            }
            
            if (i + 2 < args.Length &&
                uint.TryParse(args[i + 1], out uint readAddr) &&
                uint.TryParse(args[i + 2], out uint readLength)) {
                disallowedRead.Add((readAddr, readLength));
                i += 2;
            } else {
                Console.WriteLine("Invalid or missing values for --disallow-read flag.");
            }
            break;
        
        case "--timer":
            serialDevices.Add(0x03, new HardwareTimer());
            break;
        
        case "--disk":
            if (i + 3 >= args.Length) {
                Console.WriteLine("Invalid or missing value for --disk flag, usage: --disk <filename> <blockCount> <picosPerBlock>");
                return 1;
            }

            string fileName = args[i + 1];
            if (!long.TryParse(args[i + 2], out long blockCount)) {
                Console.WriteLine("Invalid or missing value for --disk flag, usage: --disk <filename> <blockCount> <picosPerBlock>");
                return 1;
            }
            
            if (!long.TryParse(args[i + 3], out long speed)) {
                Console.WriteLine("Invalid or missing value for --disk flag, usage: --disk <filename> <blockCount> <picosPerBlock>");
                return 1;
            }
            
            i += 3;

            FileStream file = File.Open(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            file.SetLength(blockCount * 512);
            serialDevices.Add(0x02, new Disk(file, speed));
            break;
        
        case "--vnic":  // --vnic <peer endpoint> <port>
            if (i + 2 >= args.Length) {
                Console.WriteLine("Invalid or missing value for --vnic flag, usage: --vnic <peer endpoint> <port>");
                return 1;
            }

            if (!IPEndPoint.TryParse(args[i + 1], out IPEndPoint? endpoint)) {
                Console.WriteLine("Invalid peer endpoint for --vnic flag.");
                return 1;
            }

            if (!ushort.TryParse(args[i + 2], out ushort port)) {
                Console.WriteLine("Invalid port for --vnic flag.");
                return 1;
            }
            
            // ReSharper disable twice AccessToModifiedClosure
            serialDeviceFactories.Add(0x04, vm => new VirtualNetworkCard(vm, endpoint, port));
            i += 2;
            break;
        
        case "--rcp":
            genericSerialDevices.Add(new RealityCoProcessor());
            break;
        
        default:
            Console.WriteLine($"Unknown flag: {args[i]}");
            break;
    }
}

CatVM.CatVM vm = new(memorySize, ops, File.ReadAllBytes(romPath)) {
    EnableTestingInterrupts = enableTestInts,
    DumpErrors = dumpErrors,
    ErrorOnRomWrite = errorOnRomWrite,
    DisallowedReadRegions = disallowedRead.ToArray(),
    DisallowedWriteRegions = disallowedWrite.ToArray(),
    Fast = fastRun
};

vm.SerialDevices[0] = new HardwareManager();

// add serial devices
foreach ((uint port, ISerialDevice dev) in serialDevices) {
    vm.SerialDevices[port] = dev;
}
foreach ((uint port, Func<CatVM.CatVM, ISerialDevice> factory) in serialDeviceFactories) {
    vm.SerialDevices[port] = factory(vm);
}

foreach (ISerialDevice dev in genericSerialDevices) {
    vm.RegisterSerialDevice(dev);
}

if (raylibPpu) {
    RaylibPpu ppu = new(vm);
    vm.RegisterSerialDevice(ppu.Graphics);
    vm.RegisterSerialDevice(ppu.Input);
}

CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, _) => cts.Cancel();

if (useDebugger) {
    Debugger debugger = new(vm, romPath);
    debugger.StartUserDebugging();
}
else {
    vm.Run(cts.Token);
}
Console.WriteLine("Goodbye!");
return 0;
