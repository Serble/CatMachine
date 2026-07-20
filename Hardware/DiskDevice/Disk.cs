using System.Threading.Channels;
using CatData;
using CatVM;
using CatVM.Serial;

namespace DiskDevice;

// TODO: Disk bounds checking
public class Disk : CommandBasedSerialDevice<Disk.Mode> {
    public override uint Type => 0x96818B9A;
    
    private const long BlockSize = 512;
    
    private readonly Lock _lock = new();
    private readonly Lock _streamLock = new();
    private readonly Dictionary<uint, WriteCached> _unwritten = new();
    private readonly Channel<WriteCached> _channel = Channel.CreateUnbounded<WriteCached>();
    
    private readonly Queue<(bool isRead, uint memAddr, uint startBlock, uint blockCount)> _queue;
    private bool _isRunning;
    private readonly Stream _stream;
    private readonly long _picosPerBlock;
    
    [CommandLineConstructable("Disk")]
    public Disk(string file, long picosPerBlock, long? size = null, int queueCapacity = 32, CancellationToken token = default) {
        if (size.HasValue) {
            _stream = new FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            _stream.SetLength(size.Value);
        }
        else {
            _stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite);
        }
        
        _picosPerBlock = picosPerBlock;
        _queue = new Queue<(bool isRead, uint memAddr, uint startBlock, uint blockCount)>(queueCapacity);
        
        _ = Run(token);
    }
    
    public Disk(Stream stream, long picosPerBlock, int queueCapacity = 32, CancellationToken token = default) {
        _stream = stream;
        _picosPerBlock = picosPerBlock;
        _queue = new Queue<(bool isRead, uint memAddr, uint startBlock, uint blockCount)>(queueCapacity);

        _ = Run(token);
    }

    // TODO: ask if this is how it should stop, and if it should catch exceptions
    private async Task Run(CancellationToken token) {
        List<WriteCached> written = [];
        
        while (!token.IsCancellationRequested) {
            try {
                await _channel.Reader.WaitToReadAsync(token);
            }
            catch (OperationCanceledException) {
                break;
            }
            
            while (_channel.Reader.TryRead(out WriteCached? toWrite)) {
                written.Add(toWrite);
                lock (_streamLock) {
                    _stream.Seek(toWrite.Block * BlockSize, SeekOrigin.Begin);
                    _stream.Write(toWrite.Data);
                }
            }

            if (written.Count > 0) {
                lock (_streamLock) {
                    _stream.Flush();
                }

                lock (_lock) {
                    foreach (WriteCached wrote in written) {
                        if (_unwritten.TryGetValue(wrote.Block, out WriteCached? cache) && cache.WriteId == wrote.WriteId) {
                            _unwritten.Remove(wrote.Block);
                        }
                    }
                }
                
                written.Clear();
            }
            
            Thread.Sleep(100);
        }
    }

    protected override int GetArgCount(Mode mode) {
        return mode switch {
            Mode.Read => 3,
            Mode.Write => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }
    
    protected override void RunMode(CatVm vm, Mode mode, List<uint> args) {
        switch (mode) {
            case Mode.Read: {
                _queue.Enqueue((true, args[0], args[1], args[2]));
                break;
            }
            
            case Mode.Write: {
                _queue.Enqueue((false, args[0], args[1], args[2]));
                break;
            }
            
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        if (!_isRunning) {
            _isRunning = true;
            ExecuteOperation(vm);
        }
    }

    private void ExecuteOperation(CatVm vm) {
        if (_queue.Count == 0) {
            _isRunning = false;
            return;
        }
        
        (bool isRead, uint memAddr, uint startBlock, uint blockCount) = _queue.Dequeue();
                
        void Execute() {
            if (isRead) {
                for (uint block = 0; block < blockCount; block++) {
                    int startAddr = (int)(memAddr + block * BlockSize);
                    Span<byte> span = vm.Memory.AsSpan(startAddr..(startAddr + (int)BlockSize));

                    uint absBlock = startBlock + block;
                    WriteCached? data;
                    lock (_lock) {
                        _unwritten.TryGetValue(absBlock, out data);
                    }
                    
                    if (data != null) {
                        data.Data.CopyTo(span);
                    }
                    else {
                        lock (_streamLock) {
                            _stream.Seek(absBlock * BlockSize, SeekOrigin.Begin);
                            _stream.ReadExactly(span);
                        }
                    }
                }
            }
            else {
                for (uint block = 0; block < blockCount; block++) {
                    int startAddr = (int)(memAddr + block * BlockSize);
                    Span<byte> span = vm.Memory.AsSpan(startAddr..(int)(startAddr + BlockSize));

                    uint absBlock = startBlock + block;
                    byte[] array = new byte[BlockSize];
                    span.CopyTo(array);
                    
                    lock (_lock) {
                        WriteCached cache = new(absBlock, array);
                        _unwritten[absBlock] = cache;
                        _channel.Writer.TryWrite(cache); // should never fail
                    }
                }
            }
            
            vm.HardwareInterrupt(SpecialInterrupts.DiskOperationFinish);
            
            ExecuteOperation(vm);
        }

        if (_picosPerBlock == 0) {
            Execute();
        }
        else {
            vm.RunIn(blockCount * _picosPerBlock, Execute);
        }
    }
    
    public enum Mode {
        Read  = 1,  // args: Mem Addr, Start block, Block Count
        Write = 2,  // args: Mem Addr, Start block, Block Count
    }
    
    private class WriteCached(uint block, byte[] data) {
        public readonly uint Block = block;
        public readonly byte[] Data = data;
        public readonly Guid WriteId = Guid.NewGuid();
    }
}
