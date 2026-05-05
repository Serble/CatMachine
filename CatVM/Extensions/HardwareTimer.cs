using CatVM.Serial;

namespace CatVM.Extensions;

public class HardwareTimer : ISerialDevice {
    private const uint HardwareTimerType = 0x3;
    private Mode? _mode;
    private readonly List<uint> _modeArgs = [];
    private readonly Queue<uint> _inputQueue = [];
    
    public uint Input(CatVM vm) {
        return _inputQueue.Count == 0 
            ? uint.MaxValue
            : _inputQueue.Dequeue();
    }

    public void Output(CatVM vm, uint data) {
        if (!_mode.HasValue) {
            if (data >= Enum.GetNames<Mode>().Length) {
                return;
            }
            _mode = (Mode)data;
        }
        else {
            _modeArgs.Add(data);
        }
        
        if (_modeArgs.Count < _mode switch {
                Mode.Discover => 0,
                Mode.NewTimer => 2,
                _ => throw new ArgumentOutOfRangeException()
            }) {
            return;
        }
        
        switch (_mode) {
            case Mode.Discover:
                _inputQueue.Enqueue(HardwareTimerType);
                break;
            
            case Mode.NewTimer:
                long picoseconds = _modeArgs[0] * CatVM.PicosecondsPerMillisecond;
                uint timerId = _modeArgs[1];
                vm.RunIn(picoseconds, () => {
                    _inputQueue.Enqueue(timerId);
                    vm.Interrupt(SpecialInterupts.HardwareTimerCallback);
                });
                break;
            
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        _mode = null;
        _modeArgs.Clear();
    }

    enum Mode {
        Discover,
        NewTimer
    }
}
