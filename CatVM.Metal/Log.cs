namespace CatVM.Metal;

/// <summary>
/// Console logging for the machine's own messages.
/// <p/>
/// Everything the machine says about itself is prefixed so it can be told apart from anything the
/// guest writes to the host console (interrupt <c>0x80</c>). In the bootable image these go to
/// <c>/var/log/catvm/catvm.log</c>.
/// </summary>
public static class Log {
    public static void Info(string message) {
        Console.WriteLine($"[metal] {message}");
    }

    public static void Warn(string message) {
        Console.WriteLine($"[metal] warning: {message}");
    }

    public static void Error(string message) {
        Console.Error.WriteLine($"[metal] error: {message}");
    }
}
