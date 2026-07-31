using Microsoft.Win32.SafeHandles;

namespace CatVM.Metal.Hardware;

/// <summary>
/// A <see cref="Stream"/> over a real block device, shaped so that the existing
/// <c>DiskDevice.Disk</c> hardware can sit straight on top of it.
/// <p/>
/// It exists mainly to bound the medium. The Cat disk protocol has no notion of a failed transfer,
/// so a guest asking for a block past the end of the disk must not be able to take the machine down
/// with a host IO exception. Instead this behaves the way a drive does: reads past the end come back
/// as zeroes and writes past the end are dropped.
/// <p/>
/// Reads and writes go through <see cref="RandomAccess"/> rather than a buffering
/// <see cref="FileStream"/>: every access is already 512 byte aligned, and buffering a block device
/// only adds a copy and a risk of stale data.
/// </summary>
public sealed class BlockDeviceStream : Stream {
    private readonly SafeFileHandle _handle;
    private readonly string _path;
    private long _position;
    private bool _warnedRead;
    private bool _warnedWrite;

    private BlockDeviceStream(SafeFileHandle handle, string path, long length) {
        _handle = handle;
        _path = path;
        Length = length;
    }

    /// <summary>
    /// Opens a block device (or image file) for the guest.
    /// </summary>
    /// <param name="device">The device to open.</param>
    /// <param name="syncWrites">
    /// Open with O_SYNC. Physical machines lose power without warning, and the guest is told a write
    /// completed as soon as it is queued, so by default writes are not allowed to sit in the host's
    /// page cache.
    /// </param>
    public static BlockDeviceStream Open(BlockDeviceInfo device, bool syncWrites) {
        FileOptions options = syncWrites ? FileOptions.WriteThrough : FileOptions.None;
        SafeFileHandle handle = File.OpenHandle(device.Path, FileMode.Open, FileAccess.ReadWrite,
            FileShare.ReadWrite, options);

        long length = device.SizeBytes;
        if (length <= 0) {
            // Regular files report their size here; block devices always report 0, which is why the
            // size normally comes from sysfs instead.
            try {
                length = RandomAccess.GetLength(handle);
            }
            catch (Exception) {
                length = 0;
            }
        }

        if (length <= 0) {
            Log.Warn($"{device.Path}: capacity unknown, the guest can address the whole device");
            length = long.MaxValue;
        }

        return new BlockDeviceStream(handle, device.Path, length);
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => true;

    public override long Length { get; }

    public override long Position {
        get => _position;
        set {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _position = value;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) {
        ValidateBufferArguments(buffer, offset, count);
        return Read(buffer.AsSpan(offset, count));
    }

    public override int Read(Span<byte> buffer) {
        long available = Math.Max(0, Length - _position);
        int fromDevice = (int)Math.Min(buffer.Length, available);
        int read = 0;

        while (read < fromDevice) {
            int got = RandomAccess.Read(_handle, buffer[read..fromDevice], _position + read);
            if (got <= 0) {
                break;
            }

            read += got;
        }

        if (read < buffer.Length) {
            // Off the end of the medium (or a short read at it): the guest gets zeroes.
            buffer[read..].Clear();

            if (!_warnedRead) {
                _warnedRead = true;
                Log.Warn($"{_path}: guest read past the end of the device, returning zeroes");
            }
        }

        _position += buffer.Length;
        return buffer.Length;
    }

    public override void Write(byte[] buffer, int offset, int count) {
        ValidateBufferArguments(buffer, offset, count);
        Write(buffer.AsSpan(offset, count));
    }

    public override void Write(ReadOnlySpan<byte> buffer) {
        long available = Math.Max(0, Length - _position);
        int toDevice = (int)Math.Min(buffer.Length, available);

        if (toDevice > 0) {
            RandomAccess.Write(_handle, buffer[..toDevice], _position);
        }

        if (toDevice < buffer.Length && !_warnedWrite) {
            _warnedWrite = true;
            Log.Warn($"{_path}: guest wrote past the end of the device, data dropped");
        }

        _position += buffer.Length;
    }

    public override long Seek(long offset, SeekOrigin origin) {
        long target = origin switch {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
        };

        ArgumentOutOfRangeException.ThrowIfNegative(target, nameof(offset));
        _position = target;
        return _position;
    }

    /// <summary>
    /// Nothing is buffered on this side, so there is nothing to push out. Durability comes from
    /// O_SYNC (see <see cref="Open"/>).
    /// </summary>
    public override void Flush() { }

    public override void SetLength(long value) {
        throw new NotSupportedException("The size of a physical disk cannot be changed.");
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _handle.Dispose();
        }

        base.Dispose(disposing);
    }
}
