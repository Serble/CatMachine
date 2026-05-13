using CatData;
using CatVM.Serial;

namespace CatVM.Extensions;

public class HardwareTimer : CommandBasedSerialDevice<HardwareTimer.Mode> {
    public override uint Type => 0x03;
    
    [CommandLineConstructable("Timer")]
    public HardwareTimer() {}

    protected override int GetArgCount(Mode mode) {
        return mode switch {
            Mode.NewTimer => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    protected override void RunMode(CatVm vm, Mode mode, List<uint> args) {
        switch (mode) {
            case Mode.NewTimer:
                long picoseconds = args[0] * CatVm.PicosecondsPerMillisecond;
                uint timerId = args[1];
                vm.RunIn(picoseconds, () => {
                    InputQueue.Enqueue(timerId);
                    vm.Interrupt(SpecialInterrupts.HardwareTimerCallback);
                });
                break;
            
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    public enum Mode {
        NewTimer = 1
    }
}
