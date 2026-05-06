using System.Diagnostics;
using CatVM.Serial;

namespace CatVM.Extensions;

public class Disk(Stream stream, long picosPerBlock, int queueCapacity = 32) : CommandBasedSerialDevice<Disk.Mode>, IDisposable, IAsyncDisposable {
    public override uint Type => 0x02;

    private const long BlockSize = 512;

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
                
        vm.RunIn(blockCount * picosPerBlock, () => {
            Stopwatch sw = Stopwatch.StartNew();
            stream.Seek(startBlock * BlockSize, SeekOrigin.Begin);
            Span<byte> span = vm.Memory.AsSpan((int)memAddr..(int)(memAddr + blockCount * BlockSize));
            if (isRead) {
                stream.ReadExactly(span);
            }
            else {
                stream.Write(span);
                stream.FlushAsync();
            }
            
            vm.HardwareInterrupt(SpecialInterupts.DiskOperationFinish);
            Console.WriteLine($"thing is {sw.Elapsed.TotalMilliseconds}");
            
            ExecuteOperation(vm);
        });
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