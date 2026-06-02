using System.Collections.Concurrent;
using CatData;
using CatVM.Serial;

namespace CatVM.Extensions;

public class ConsoleSerialMonitor : ISerialDevice {
    public uint Type => 0xBBAC8C8C;

    private readonly ConcurrentQueue<char> _output = new();

    [CommandLineConstructable("serialmonitor")]
    public ConsoleSerialMonitor(CancellationToken token = default) {
        Task.Run(() => {
            while (!token.IsCancellationRequested) {
                if (_output.TryDequeue(out char data)) {
                    Console.Write(data);
                }
                Thread.Sleep(1);
            }
        }, token);
    }

    public uint Input(CatVm vm) {
        return 0;
    }

    public void Output(CatVm vm, uint data) {
        _output.Enqueue((char)data);
    }
}
