using CatArgs.Args;
using CatLauncher.Args;

namespace CatLauncher;

public class RunArguments {
    public Dictionary<string, SerialDeviceArgument> DeviceArgs { get; }

    public readonly RomArgument Rom = new("rom", "r");
    public readonly FlagArgument Fast = new("fast", "f");
    public readonly IntArgument Ops = new(["ops", "o"], 100_000);
    public readonly IntArgument Memory = new(["memory", "m"], 1024 * 1024 * 16, 1, int.MaxValue);
    public readonly FlagArgument TestInts = new("test-ints");
    public readonly FlagArgument DumpErrors = new("dump-errors");
    public readonly FlagArgument DisableHardwareManager = new("disable-hardware-manager");
    public readonly DevicesArgument Devices;

#if DEBUG
    public readonly FlagArgument ProtectRom = new("protect-rom");
    public readonly MemDisallowArgument DisallowWrites = new("disallow-write");
    public readonly MemDisallowArgument DisallowReads = new("disallow-read");
#endif

    public RunArguments(Dictionary<string, SerialDeviceArgument> deviceArgs) {
        Devices = new DevicesArgument(this, "device", "d");
        DeviceArgs = deviceArgs;
    }
}
