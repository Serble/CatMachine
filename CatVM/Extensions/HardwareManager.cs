using CatVM.Serial;

namespace CatVM.Extensions;

public class HardwareManager : CommandBasedSerialDevice<HardwareManager.Mode> {
    public override uint Type => 0;
    
    protected override int GetArgCount(Mode mode) {
        return mode switch {
            Mode.ListDevices => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
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
            
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }
    
    public enum Mode {
        ListDevices = 1
    }
}
