namespace CatVM.AotTest.Tests;

/// <summary>MOV / MOV16 / MOV8 across all addressing modes plus fault cases.</summary>
public static class MovTests {
    public static void Register(TestRunner r) {
        const string g = "MOV";

        r.Add(g, "MOV R,R", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0x12345678;
            h.Execute(0x00, 0x02, 0x01);
            Check.Equal(0x12345678u, h.Vm.Cpu.R2);
        });

        r.Add(g, "MOV R,I", () => {
            VmHarness h = new();
            h.Execute(0x01, 0x02, 0x78, 0x56, 0x34, 0x12);
            Check.Equal(0x12345678u, h.Vm.Cpu.R2);
        });

        r.Add(g, "MOV R,[R]", () => {
            VmHarness h = new();
            h.Vm.Memory[0x30] = 0x78; h.Vm.Memory[0x31] = 0x56;
            h.Vm.Memory[0x32] = 0x34; h.Vm.Memory[0x33] = 0x12;
            h.Vm.Cpu.R1 = 0x30;
            h.Execute(0x02, 0x02, 0x01);
            Check.Equal(0x12345678u, h.Vm.Cpu.R2);
        });

        r.Add(g, "MOV R,[I]", () => {
            VmHarness h = new();
            h.Vm.Memory[0x30] = 0x78; h.Vm.Memory[0x31] = 0x56;
            h.Vm.Memory[0x32] = 0x34; h.Vm.Memory[0x33] = 0x12;
            h.Execute(0x03, 0x02, 0x30, 0x00, 0x00, 0x00);
            Check.Equal(0x12345678u, h.Vm.Cpu.R2);
        });

        r.Add(g, "MOV [R],R", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0x30;
            h.Vm.Cpu.R2 = 0x12345678;
            h.Execute(0x04, 0x01, 0x02);
            Check.Equal(0x12345678u, BitConverter.ToUInt32(h.Vm.Memory, 0x30));
        });

        r.Add(g, "MOV [I],I", () => {
            VmHarness h = new();
            h.Execute(0x07, 0x30, 0x00, 0x00, 0x00, 0x78, 0x56, 0x34, 0x12);
            Check.Equal(0x12345678u, BitConverter.ToUInt32(h.Vm.Memory, 0x30));
        });

        r.Add(g, "MOV16 R,[R]", () => {
            VmHarness h = new();
            h.Vm.Memory[0x30] = 0x78; h.Vm.Memory[0x31] = 0x56;
            h.Vm.Cpu.R1 = 0x30;
            h.Execute(0x08, 0x02, 0x01);
            Check.Equal(0x5678u, h.Vm.Cpu.R2);
        });

        r.Add(g, "MOV16 [R],R stores low 16 bits", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0x30;
            h.Vm.Cpu.R2 = 0x12345678;
            h.Execute(0x0A, 0x01, 0x02);
            Check.Equal((ushort)0x5678, BitConverter.ToUInt16(h.Vm.Memory, 0x30));
        });

        r.Add(g, "MOV8 R,[R]", () => {
            VmHarness h = new();
            h.Vm.Memory[0x30] = 0x78;
            h.Vm.Cpu.R1 = 0x30;
            h.Execute(0x0E, 0x02, 0x01);
            Check.Equal(0x78u, h.Vm.Cpu.R2);
        });

        r.Add(g, "MOV8 [I],I stores single byte", () => {
            VmHarness h = new();
            h.Execute(0x13, 0x30, 0x00, 0x00, 0x00, 0x78);
            Check.Equal((byte)0x78, h.Vm.Memory[0x30]);
        });

        r.Add(g, "BMov zero-extends byte", () => {
            VmHarness h = new();
            h.Vm.Memory[0x30] = 0xFF;
            h.Vm.Cpu.R2 = 0xAAAAAAAA;
            h.Execute(0x0F, 0x02, 0x30, 0x00, 0x00, 0x00);
            Check.Equal(0xFFu, h.Vm.Cpu.R2);
        });

        r.Add(g, "SMov zero-extends short", () => {
            VmHarness h = new();
            h.Vm.Memory[0x30] = 0xFF; h.Vm.Memory[0x31] = 0xFF;
            h.Vm.Cpu.R2 = 0xAAAAAAAA;
            h.Execute(0x09, 0x02, 0x30, 0x00, 0x00, 0x00);
            Check.Equal(0xFFFFu, h.Vm.Cpu.R2);
        });

        r.Add(g, "MOV unaligned 32-bit write works", () => {
            VmHarness h = new();
            h.Execute(0x07, 0x33, 0x00, 0x00, 0x00, 0x78, 0x56, 0x34, 0x12);
            Check.Equal(0x12345678u, BitConverter.ToUInt32(h.Vm.Memory, 0x33));
        });

        r.Add(g, "MOV out-of-range read raises page fault", () => {
            VmHarness h = new();
            h.Vm.LoadData([0x03, 0x02, 0x00, 0xF0, 0xFF, 0xFF]);
            h.Vm.Cpu.Ip = 0;
            h.Vm.ExecuteWithErrorHandling(() => h.Vm.ExecuteInstruction(fast: true));
            Check.True(h.Vm.Paused);
        });

        r.Add(g, "MOV in virtual mode uses MBase for fetch", () => {
            VmHarness h = new();
            h.Vm.Reset();
            const uint mbase = 0x100;
            h.Vm.LoadData([0x01, 0x02, 0x78, 0x56, 0x34, 0x12], mbase);
            h.Vm.Cpu.MBase = mbase;
            h.Vm.Cpu.MLen = 0x80;
            h.Vm.Cpu.Sp = 0x80;
            h.Vm.Cpu.Mode = 0b01;
            h.Vm.Cpu.Ip = 0;
            h.Vm.ExecuteInstruction();
            Check.Equal(0x12345678u, h.Vm.Cpu.R2);
            Check.Equal(6u, h.Vm.Cpu.Ip);
        });

        r.Add(g, "Virtual-mode IP beyond MLen raises page fault", () => {
            VmHarness h = new();
            h.Vm.Reset();
            h.Vm.Cpu.MBase = 0x40;
            h.Vm.Cpu.MLen = 0x10;
            h.Vm.Cpu.Sp = 0x10;
            h.Vm.Cpu.Mode = 0b01;
            h.Vm.Cpu.Ip = 0x10;
            h.Vm.ExecuteWithErrorHandling(() => h.Vm.ExecuteInstruction(fast: true));
            Check.True(h.Vm.Paused);
        });
    }
}
