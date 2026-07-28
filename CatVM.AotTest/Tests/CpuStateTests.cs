namespace CatVM.AotTest.Tests;

/// <summary>Register get/set, flag bits, mode bits, RegRef, equality and dump.</summary>
public static class CpuStateTests {
    public static void Register(TestRunner r) {
        const string g = "CatCpuState";

        r.Add(g, "Get returns each field", () => {
            CatCpuState cpu = new() {
                R0 = 0x00, R1 = 0x11, R2 = 0x22, R3 = 0x33,
                R4 = 0x44, R5 = 0x55, R6 = 0x66, R7 = 0x77,
                Sp = 0x88, Ip = 0x99, Fl = 0xAA,
            };
            for (byte i = 0; i < 8; i++) Check.Equal((uint)(i * 0x11), cpu.Get(i), $"R{i}");
            Check.Equal(0x88u, cpu.Get(8), "Sp");
            Check.Equal(0x99u, cpu.Get(9), "Ip");
            Check.Equal(0xAAu, cpu.Get(10), "Fl");
        });

        r.Add(g, "Set writes each field", () => {
            CatCpuState cpu = new();
            for (byte i = 0; i < 11; i++) cpu.Set(i, (uint)(i * 0x10));
            Check.Equal(0x00u, cpu.R0);
            Check.Equal(0x70u, cpu.R7);
            Check.Equal(0x80u, cpu.Sp);
            Check.Equal(0x90u, cpu.Ip);
            Check.Equal(0xA0u, cpu.Fl);
        });

        r.Add(g, "Get out of range throws", () => {
            CatCpuState cpu = new();
            Check.Throws<ArgumentOutOfRangeException>(() => cpu.Get(11));
            Check.Throws<ArgumentOutOfRangeException>(() => cpu.Get(0xFF));
        });

        r.Add(g, "Set out of range throws", () => {
            CatCpuState cpu = new();
            Check.Throws<ArgumentOutOfRangeException>(() => cpu.Set(11, 0));
        });

        r.Add(g, "RegRef allows mutation", () => {
            CatCpuState cpu = new() { R3 = 5 };
            ref uint reg = ref cpu.RegRef(3);
            reg += 7;
            Check.Equal(12u, cpu.R3);
        });

        r.Add(g, "RegRef out of range throws", () => {
            CatCpuState cpu = new();
            Check.Throws<ArgumentOutOfRangeException>(() => { _ = cpu.RegRef(11); });
        });

        r.Add(g, "Flag bits round-trip independently", () => {
            CatCpuState cpu = new();
            cpu.ZeroFlag = true;
            Check.Equal(0x01u, cpu.Fl);
            cpu.ZeroFlag = false;
            Check.Equal(0x00u, cpu.Fl);
            cpu.CarryFlag = true;
            Check.Equal(0x02u, cpu.Fl);
            cpu.SignFlag = true;
            Check.Equal(0x06u, cpu.Fl);
            cpu.OverflowFlag = true;
            Check.Equal(0x0Eu, cpu.Fl);
            cpu.CarryFlag = false;
            Check.Equal(0x0Cu, cpu.Fl);
        });

        r.Add(g, "Flag bits preserve unrelated high bits", () => {
            CatCpuState cpu = new() { Fl = 0xDEADBE00 };
            cpu.ZeroFlag = true;
            cpu.CarryFlag = true;
            cpu.SignFlag = true;
            cpu.OverflowFlag = true;
            Check.Equal(0xDEADBE0Fu, cpu.Fl);
            cpu.OverflowFlag = false;
            Check.Equal(0xDEADBE07u, cpu.Fl);
        });

        r.Add(g, "Mode bits virtual/supervisor independent", () => {
            CatCpuState cpu = new();
            cpu.VirtualMode = true;
            Check.Equal((byte)0b01, cpu.Mode);
            Check.False(cpu.SupervisorMode);
            cpu.SupervisorMode = true;
            Check.Equal((byte)0b11, cpu.Mode);
            cpu.VirtualMode = false;
            Check.Equal((byte)0b10, cpu.Mode);
            cpu.SupervisorMode = false;
            Check.Equal((byte)0, cpu.Mode);
        });

        r.Add(g, "Mode bits preserve high bits", () => {
            CatCpuState cpu = new() { Mode = 0b11110000 };
            cpu.VirtualMode = true;
            Check.Equal((byte)0b11110001, cpu.Mode);
            cpu.VirtualMode = false;
            Check.Equal((byte)0b11110000, cpu.Mode);
        });

        r.Add(g, "Equals true when all fields match", () => {
            CatCpuState a = new() {
                R0 = 1, R1 = 2, R3 = 3, Sp = 9, Ip = 4, Fl = 0xF,
                Mode = 0b01, It = 0x100, Ksp = 0x200, MBase = 0x300, MLen = 0x80,
            };
            CatCpuState b = a;
            Check.True(a.Equals(b));
            Check.True(a.Equals((object)b));
            Check.Equal(a.GetHashCode(), b.GetHashCode());
        });

        r.Add(g, "Equals false when a field differs", () => {
            CatCpuState a = new() { R5 = 5 };
            CatCpuState b = new() { R5 = 6 };
            Check.False(a.Equals(b));
            Check.False(a.Equals((object)"not a cpu"));
            Check.False(a.Equals((object?)null));
        });

        r.Add(g, "Dump contains registers and mode name", () => {
            CatCpuState cpu = new() { R0 = 0xAA, Mode = 0b01 };
            string s = cpu.Dump();
            Check.True(s.Contains("R0: 0x000000AA"), "dump should contain R0");
            Check.True(s.Contains("User"), "dump should contain mode name");
            Check.Equal(s, cpu.ToString());
        });
    }
}
