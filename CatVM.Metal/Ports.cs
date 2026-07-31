namespace CatVM.Metal;

/// <summary>
/// The serial port map of the Metal machine.
/// <p/>
/// Unlike the reference launcher, where ports depend on the order devices were passed on the
/// command line, a physical machine has a fixed layout. Guests may hard code these ports, or
/// discover them through the hardware manager on port 0 like the firmware does.
/// </summary>
public static class Ports {
    /// <summary>Hardware manager. Always port 0, as the serial protocol requires.</summary>
    public const uint HardwareManager = 0;

    /// <summary>The PPU's graphics device (the display).</summary>
    public const uint Graphics = 1;

    /// <summary>The PPU's keyboard input device.</summary>
    public const uint Keyboard = 2;

    /// <summary>The PPU's mouse input device.</summary>
    public const uint Mouse = 3;

    /// <summary>Hardware timer.</summary>
    public const uint Timer = 4;

    /// <summary>
    /// The first disk. Further disks follow on consecutive ports in the order they were
    /// discovered (or given on the command line).
    /// </summary>
    public const uint FirstDisk = 16;
}
