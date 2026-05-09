namespace CatVM.Serial;

public abstract class CommandBasedSerialDevice<T> : ISerialDevice where T : struct, Enum {
    public abstract uint Type { get; }

    protected virtual bool AutoDiscovery => true;
    
    private T? _mode;
    private readonly List<uint> _modeArgs = [];
    protected readonly Queue<uint> InputQueue = new();
    
    protected abstract int GetArgCount(T mode);
    protected abstract void RunMode(CatVM vm, T mode, List<uint> args);
    
    public virtual uint Input(CatVM vm) {
        return InputQueue.Count == 0 ? uint.MaxValue : InputQueue.Dequeue();
    }
    
    public virtual void Output(CatVM vm, uint data) {
        if (!_mode.HasValue) {
            if (AutoDiscovery && data == 0) {
                InputQueue.Enqueue(Type);
                return;
            }
            
            if (!Enum.IsDefined(typeof(T), (int)data)) {
                return;
            }
            
            _mode = (T)Enum.ToObject(typeof(T), (int)data);
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