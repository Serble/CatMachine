namespace CatVM;

public struct CatCpu {
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
    
    public CatCpu() {
        
    }

    public void Set(byte register, uint value) {
        switch (register) {
            case 0x00:
                R0 = value;
                break;
            case 0x01:
                R1 = value;
                break;
            case 0x02:
                R2 = value;
                break;
            case 0x03:
                R3 = value;
                break;
            case 0x04:
                R4 = value;
                break;
            case 0x05:
                R5 = value;
                break;
            case 0x06:
                R6 = value;
                break;
            case 0x07:
                R7 = value;
                break;
            case 0x08:
                Sp = value;
                break;
            case 0x09:
                Ip = value;
                break;
            case 0x0A:
                Fl = value;
                break;
            case 0x0B:
                It = value;
                break;
            
            default:
                throw new ArgumentOutOfRangeException(nameof(register), "Invalid register: " + register);
        }
    }
    
    public uint Get(byte register) {
        return register switch {
            0x00 => R0,
            0x01 => R1,
            0x02 => R2,
            0x03 => R3,
            0x04 => R4,
            0x05 => R5,
            0x06 => R6,
            0x07 => R7,
            0x08 => Sp,
            0x09 => Ip,
            0x0A => Fl,
            0x0B => It,
            _ => throw new ArgumentOutOfRangeException(nameof(register), "Invalid register: " + register)
        };
    }

    public string Dump() {
        return $"R0: 0x{R0:X8} R1: 0x{R1:X8} R2: 0x{R2:X8} R3: 0x{R3:X8} R4: 0x{R4:X8} R5: 0x{R5:X8} R6: 0x{R6:X8} " +
               $"R7: 0x{R7:X8} Sp: 0x{Sp:X8} Ip: 0x{Ip:X8} Fl: 0x{Fl:X8} It: 0x{It:X8}";
    }
}
