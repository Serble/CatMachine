namespace CatVM.AotTest.Tests;

/// <summary>AND, OR, XOR, NOT and SHL/SHR bitwise operations.</summary>
public static class LogicTests {
    public static void Register(TestRunner r) {
        const string g = "Bitwise";

        r.Add(g, "AND R,R", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 0b1100; h.Vm.Cpu.R5 = 0b1010;
            h.Execute(0x2b, 0x04, 0x05);
            Check.Equal(0b1000u, h.Vm.Cpu.R4);
        });

        r.Add(g, "AND R,I mask", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 0b1111;
            h.Execute(0x2c, 0x04, 0b0111, 0x00, 0x00, 0x00);
            Check.Equal(0b0111u, h.Vm.Cpu.R4);
        });

        r.Add(g, "OR R,R", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0b1100; h.Vm.Cpu.R2 = 0b0011;
            h.Execute(0x29, 0x01, 0x02);
            Check.Equal(0b1111u, h.Vm.Cpu.R1);
        });

        r.Add(g, "XOR R,R", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0b1100; h.Vm.Cpu.R2 = 0b1010;
            h.Execute(0x2d, 0x01, 0x02);
            Check.Equal(0b0110u, h.Vm.Cpu.R1);
        });

        r.Add(g, "XOR self clears register", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0xDEADBEEF;
            h.Execute(0x2d, 0x01, 0x01);
            Check.Equal(0u, h.Vm.Cpu.R1);
        });

        r.Add(g, "NOT inverts all bits", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0x00000000;
            h.Execute(0x2f, 0x01);
            Check.Equal(0xFFFFFFFFu, h.Vm.Cpu.R1);
        });

        r.Add(g, "SHL R,R", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0b10101010; h.Vm.Cpu.R2 = 3;
            h.Execute(0x4e, 0x01, 0x02);
            Check.Equal(0b10101010u << 3, h.Vm.Cpu.R1);
        });

        r.Add(g, "SHR R,I", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0b10101010;
            h.Execute(0x51, 0x01, 0x03, 0x00, 0x00, 0x00);
            Check.Equal(0b10101u, h.Vm.Cpu.R1);
        });

        r.Add(g, "SHL by 31", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 1;
            h.Execute(0x4f, 0x01, 31, 0x00, 0x00, 0x00);
            Check.Equal(0x80000000u, h.Vm.Cpu.R1);
        });

        r.Add(g, "SHL by zero is identity", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0xDEADBEEF;
            h.Execute(0x4f, 0x01, 0x00, 0x00, 0x00, 0x00);
            Check.Equal(0xDEADBEEFu, h.Vm.Cpu.R1);
        });
    }
}
