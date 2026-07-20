using CatData;
using CatVM;
using CatVM.Serial;

namespace HelloWorldDevice;

public class HelloWorldSerialDevice : ISerialDevice {
    public uint Type => 0xC0D370A1;

    [CommandLineConstructable("HelloWorld")]
    public HelloWorldSerialDevice(CatVm vm) {
        // woohoo, let's register normally, and also at 122
        vm.RegisterSerialDevice(122, this);
    }

    public uint Input(CatVm vm) {
        const int inp = 5;
        Console.WriteLine($"Hello World, sending input: {inp}");
        return inp;
    }

    public void Output(CatVm vm, uint data) {
        Console.WriteLine($"Hello World: {data}");
    }
}
