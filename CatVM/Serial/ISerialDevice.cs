namespace CatVM.Serial;

public interface ISerialDevice {
    /// <summary>
    /// Called when the application reads from the device.
    /// The device should return an uint value as the result of the read operation.
    /// </summary>
    uint Input(CatVM vm);

    /// <summary>
    /// Called when the application writes to the device.
    /// The device receives the value being written as a parameter.
    /// </summary>
    void Output(CatVM vm, uint data);
    
    /// <summary>
    /// Creates a new serial device with the specified input and output functions.
    /// </summary>
    /// <param name="input">The <see cref="Input"/> function.</param>
    /// <param name="output">The <see cref="Output"/> function.</param>
    /// <returns>The new serial device.</returns>
    public static ISerialDevice Create(Func<CatVM, uint> input, Action<CatVM, uint> output) =>
        new SerialDevice(input, output);
    
    /// <summary>
    /// The null serial device that always returns uint.MaxValue on input and ignores all output.
    /// </summary>
    public static ISerialDevice Null => new SerialDevice(
        _ => uint.MaxValue,
        (_, _) => {}
    );
}

/// <summary>
/// Generic implementation of <see cref="ISerialDevice"/> that takes input and output functions as constructor parameters.
/// </summary>
/// <param name="Input">The <see cref="ISerialDevice.Input"/> function.</param>
/// <param name="Output">The <see cref="ISerialDevice.Output"/> function.</param>
public record SerialDevice(Func<CatVM, uint> Input, Action<CatVM, uint> Output) : ISerialDevice {
    uint ISerialDevice.Input(CatVM vm) {
        return Input(vm);
    }

    void ISerialDevice.Output(CatVM vm, uint data) {
        Output(vm, data);
    }
}
