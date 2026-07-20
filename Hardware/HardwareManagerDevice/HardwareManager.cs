using CatData;
using CatVM;
using CatVM.Serial;

namespace HardwareManagerDevice;

public class HardwareManager : CommandBasedSerialDevice<HardwareManager.Mode> {
    public override uint Type => 0x296C4EF5;
    
    [CommandLineConstructable("HardwareManager")]
    public HardwareManager() {}
    
    protected override int GetArgCount(Mode mode) {
        return mode switch {
            Mode.ListDevices => 0,
            Mode.HaltSystem => 0,
            Mode.ShutdownSystem => 0,
            Mode.ResetSystem => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    public override void Output(CatVm vm, uint data) {
        InputQueue.Clear();
        base.Output(vm, data);
    }

    protected override void RunMode(CatVm vm, Mode mode, List<uint> args) {
        switch (mode) {
            case Mode.ListDevices: {
                InputQueue.Enqueue((uint)vm.SerialDevices.Count);
                foreach ((uint port, ISerialDevice device) in vm.SerialDevices) {
                    InputQueue.Enqueue(port);
                    InputQueue.Enqueue(device.Type);
                }
                break;
            }

            case Mode.HaltSystem:
                vm.Paused = true;
                break;

            case Mode.ShutdownSystem:
                vm.Shutdown();
                break;

            case Mode.ResetSystem:
                vm.Reset();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }
    
    public enum Mode {
        ListDevices = 1,
        HaltSystem = 2,
        ShutdownSystem = 3,
        ResetSystem = 4
    }
}
