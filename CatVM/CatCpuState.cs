using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace CatVM;

// Explicit sequential layout so the Unsafe.Add-based register lookup in Get/Set/RegRef is sound:
// register N maps to the Nth uint at offset N*4 from the start of the struct.
[StructLayout(LayoutKind.Sequential)]
public struct CatCpuState {
    public uint R0;
    public uint R1;
    public uint R2;
    public uint R3;
    public uint R4;
    public uint R5;
    public uint R6;
    public uint R7;
    public uint Sp;  // Stack pointer
    public uint Ip;  // Instruction pointer
    public uint Fl;  // Flags register         - bit 0: zero flag, bit 1: carry flag, bit 2: sign flag, bit 3: overflow flag
    public uint It = uint.MaxValue;  // Interrupt table pointer

    public bool ZeroFlag {
        get => (Fl & 0x01) != 0;
        set {
            if (value) {
                Fl |= 0x01;
            } else {
                Fl &= 0xFFFFFFFE;
            }
        }
    }
    
    public bool CarryFlag {
        get => (Fl & 0x02) != 0;
        set {
            if (value) {
                Fl |= 0x02;
            } else {
                Fl &= 0xFFFFFFFD;
            }
        }
    }
    
    public bool SignFlag {
        get => (Fl & 0x04) != 0;
        set {
            if (value) {
                Fl |= 0x04;
            } else {
                Fl &= 0xFFFFFFFB;
            }
        }
    }
    
    public bool OverflowFlag {
        get => (Fl & 0x08) != 0;
        set {
            if (value) {
                Fl |= 0x08;
            } else {
                Fl &= 0xFFFFFFF7;
            }
        }
    }
    
    public CatCpuState() {
        
    }

    /// <summary>Number of accessible registers (R0..R7, Sp, Ip, Fl, It).</summary>
    private const int RegisterCount = 12;

    public void Set(byte register, uint value) {
        if (register >= RegisterCount) ThrowInvalidRegister(register);
        Unsafe.Add(ref Unsafe.As<CatCpuState, uint>(ref this), register) = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Get(byte register) {
        if (register >= RegisterCount) ThrowInvalidRegister(register);
        return Unsafe.Add(ref Unsafe.As<CatCpuState, uint>(ref this), register);
    }

    /// <summary>
    /// Returns a ref to the register's storage. Lets callers read-modify-write a register without
    /// going through a switch twice. Throws <see cref="ArgumentOutOfRangeException"/> for invalid
    /// register indices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [UnscopedRef]
    public ref uint RegRef(byte register) {
        if (register >= RegisterCount) ThrowInvalidRegister(register);
        return ref Unsafe.Add(ref Unsafe.As<CatCpuState, uint>(ref this), register);
    }

    // Throw helper kept out-of-line so the JIT can inline the hot path of Get/Set/RegRef.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidRegister(byte register) =>
        throw new ArgumentOutOfRangeException(nameof(register), "Invalid register: " + register);

    public string Dump() {
        return $"R0: 0x{R0:X8} R1: 0x{R1:X8} R2: 0x{R2:X8} R3: 0x{R3:X8} R4: 0x{R4:X8} R5: 0x{R5:X8} R6: 0x{R6:X8} " +
               $"R7: 0x{R7:X8} Sp: 0x{Sp:X8} Ip: 0x{Ip:X8} Fl: 0x{Fl:X8} It: 0x{It:X8}";
    }

    public void SaveState(Stream stream) {
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write(R0);
        writer.Write(R1);
        writer.Write(R2);
        writer.Write(R3);
        writer.Write(R4);
        writer.Write(R5);
        writer.Write(R6);
        writer.Write(R7);
        writer.Write(Sp);
        writer.Write(Ip);
        writer.Write(Fl);
        writer.Write(It);
    }
    
    public static CatCpuState LoadState(Stream stream) {
        CatCpuState state = new();
        using BinaryReader reader = new(stream, Encoding.UTF8, true);
        state.R0 = reader.ReadUInt32();
        state.R1 = reader.ReadUInt32();
        state.R2 = reader.ReadUInt32();
        state.R3 = reader.ReadUInt32();
        state.R4 = reader.ReadUInt32();
        state.R5 = reader.ReadUInt32();
        state.R6 = reader.ReadUInt32();
        state.R7 = reader.ReadUInt32();
        state.Sp = reader.ReadUInt32();
        state.Ip = reader.ReadUInt32();
        state.Fl = reader.ReadUInt32();
        state.It = reader.ReadUInt32();
        return state;
    }

    public override bool Equals([NotNullWhen(true)] object? obj) {
        if (obj is not CatCpuState other) return false;
        return R0 == other.R0 && R1 == other.R1 && R2 == other.R2 && R3 == other.R3 &&
               R4 == other.R4 && R5 == other.R5 && R6 == other.R6 && R7 == other.R7 &&
               Sp == other.Sp && Ip == other.Ip && Fl == other.Fl && It == other.It;
    }

    public override int GetHashCode() {
        return HashCode.Combine(HashCode.Combine(R0, R1, R2, R3, R4, R5, R6, R7), Sp, Ip, Fl, It);
    }

    public override string ToString() {
        return Dump();
    }
}
