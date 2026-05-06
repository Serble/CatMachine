namespace CatVM.Serial;

public abstract class CommandBasedSerialDevice<T> : ISerialDevice where T : struct, Enum {
    public abstract uint Type { get; }
    private T? _mode;
    private readonly List<uint> _modeArgs = [];
    protected readonly Queue<uint> InputQueue = new();
    
    protected abstract int GetArgCount(T mode);
    protected abstract void RunMode(CatVM vm, T mode, List<uint> args);
    
    public uint Input(CatVM vm) {
        return InputQueue.Count == 0 ? uint.MaxValue : InputQueue.Dequeue();
    }
    
    public void Output(CatVM vm, uint data) {
        if (!_mode.HasValue) {
            if (data == 0) {
                InputQueue.Enqueue(Type);
                return;
            }
            
            if (Enum.IsDefined(typeof(T), data)) {
                return;
            }
            
            _mode = (T?)Enum.ToObject(typeof(T), data);
        }
        else {
            _modeArgs.Add(data);
        }
        
        if (_modeArgs.Count < GetArgCount(_mode.Value)) {
            return;
        }
        
        RunMode(vm, _mode.Value, _modeArgs);
        
        _mode = null;
        _modeArgs.Clear();
    }
}