using System.Diagnostics;
using CatVM.Serial;

namespace CatVM.Extensions;

public class Disk(Stream stream, long picosPerBlock, int queueCapacity = 32) : CommandBasedSerialDevice<Disk.Mode>, IDisposable, IAsyncDisposable {
    public override uint Type => 0x02;

    private const long BlockSize = 512;

    private Dictionary<uint, byte[]> _unwrittenData = [];
    private Queue<(bool isRead, uint memAddr, uint startBlock, uint blockCount)> _queue = new(queueCapacity);
    private bool _isRunning;

    protected override int GetArgCount(Mode mode) {
        return mode switch {
            Mode.Read => 3,
            Mode.Write => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }
    
    protected override void RunMode(CatVM vm, Mode mode, List<uint> args) {
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

    private void ExecuteOperation(CatVM vm) {
        if (_queue.Count == 0) {
            _isRunning = false;
            return;
        }
        
        (bool isRead, uint memAddr, uint startBlock, uint blockCount) = _queue.Dequeue();
                
        void Execute() {
            Stopwatch sw = Stopwatch.StartNew();
            if (isRead) {
                for (uint block = startBlock; block < startBlock + blockCount; block++) {
                    int startAddr = (int)(memAddr + block * BlockSize);
                    Span<byte> span = vm.Memory.AsSpan(startAddr..(startAddr + (int)BlockSize));
                    
                    if (_unwrittenData.TryGetValue(block, out byte[]? data)) {
                        data.CopyTo(span);
                    }
                    else {
                        stream.Seek(block * BlockSize, SeekOrigin.Begin);
                        stream.ReadExactly(span);
                    }
                }
            }
            else {
                for (uint block = startBlock; block < startBlock + blockCount; block++) {
                    int startAddr = (int)(memAddr + block * BlockSize);
                    Span<byte> span = vm.Memory.AsSpan(startAddr..(int)(startAddr + BlockSize));

                    if (_unwrittenData.TryGetValue(block, out byte[]? array)) {
                        span.CopyTo(array);
                    }
                    else {
                        array = new byte[BlockSize];
                        span.CopyTo(array);
                        _unwrittenData[block] = array;
                    }
                }
            }
            
            vm.HardwareInterrupt(SpecialInterupts.DiskOperationFinish);
            Console.WriteLine($"thing is {sw.Elapsed.TotalMilliseconds}");
            
            ExecuteOperation(vm);
        }

        if (picosPerBlock == 0) {
            Execute();
        }
        else {
            vm.RunIn(blockCount * picosPerBlock, Execute);
        }
    }
    
    public enum Mode {
        Read  = 1,  // args: Mem Addr, Start block, Block Count
        Write = 2,  // args: Mem Addr, Start block, Block Count
    }

    public void Dispose() {
        stream.Flush();
        stream.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync() {
        await stream.FlushAsync();
        await stream.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}