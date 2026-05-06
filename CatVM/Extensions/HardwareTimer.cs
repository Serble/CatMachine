using CatVM.Serial;

namespace CatVM.Extensions;

public class HardwareTimer : CommandBasedSerialDevice<HardwareTimer.Mode> {
    public override uint Type => 0x03;

    protected override int GetArgCount(Mode mode) {
        return mode switch {
            Mode.NewTimer => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    protected override void RunMode(CatVM vm, Mode mode, List<uint> args) {
        switch (mode) {
            case Mode.NewTimer:
                long picoseconds = args[0] * CatVM.PicosecondsPerMillisecond;
                uint timerId = args[1];
                vm.RunIn(picoseconds, () => {
                    InputQueue.Enqueue(timerId);
                    vm.Interrupt(SpecialInterupts.HardwareTimerCallback);
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
