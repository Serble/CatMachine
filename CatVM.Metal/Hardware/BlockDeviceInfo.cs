namespace CatVM.Metal.Hardware;

/// <summary>
/// A block device the machine can hand to the guest as a Cat disk.
/// </summary>
/// <param name="Path">Path of the device node (or image file).</param>
/// <param name="Name">Kernel name, e.g. <c>sda</c>. Empty for paths outside /dev.</param>
/// <param name="SizeBytes">Capacity, or 0 if it could not be determined.</param>
/// <param name="Model">Hardware model string, when the kernel exposes one.</param>
/// <param name="Removable">Whether the kernel reports the device as removable.</param>
public sealed record BlockDeviceInfo(string Path, string Name, long SizeBytes, string? Model, bool Removable) {
    /// <summary>Capacity in 512 byte Cat disk blocks.</summary>
    public long Blocks => SizeBytes / 512;

    public string Describe() {
        string size = SizeBytes > 0
            ? $"{SizeBytes / (1024.0 * 1024.0):F0} MiB, {Blocks} blocks"
            : "size unknown";
        string model = string.IsNullOrWhiteSpace(Model) ? "" : $", {Model}";
        string removable = Removable ? ", removable" : "";
        return $"{Path} ({size}{model}{removable})";
    }
}
