namespace CatVM.AotTest.Tests;

/// <summary>PUSH/POP at all widths, CALL/RET and stack-pointer bookkeeping.</summary>
public static class StackTests {
    public static void Register(TestRunner r) {
        const string g = "Stack";

        r.Add(g, "PUSH R then StackPop", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0x12345678;
            h.Execute(0x20, 0x01);
            Check.Equal(0x12345678u, h.Vm.StackPop());
        });

        r.Add(g, "PUSH I then StackPop", () => {
            VmHarness h = new();
            h.Execute(0x21, 0x78, 0x56, 0x34, 0x12);
            Check.Equal(0x12345678u, h.Vm.StackPop());
        });

        r.Add(g, "PUSH16 then StackPop16", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0x12345678;
            h.Execute(0x22, 0x01);
            Check.Equal((ushort)0x5678, h.Vm.StackPop16());
        });

        r.Add(g, "PUSH8 then StackPop8", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0x12345678;
            h.Execute(0x24, 0x01);
            Check.Equal((byte)0x78, h.Vm.StackPop8());
        });

        r.Add(g, "POP R reads pushed value", () => {
            VmHarness h = new();
            h.Vm.StackPush(0x12345678u);
            h.Execute(0x26, 0x01);
            Check.Equal(0x12345678u, h.Vm.Cpu.R1);
        });

        r.Add(g, "PUSH/POP round trip", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0xDEADBEEF;
            h.Vm.LoadData([0x20, 0x01, 0x26, 0x02]);
            h.Vm.Cpu.Ip = 0;
            h.Vm.ExecuteInstruction();
            h.Vm.ExecuteInstruction();
            Check.Equal(0xDEADBEEFu, h.Vm.Cpu.R2);
        });

        r.Add(g, "PUSH decrements SP by width", () => {
            VmHarness h = new();
            uint before = h.Vm.Cpu.Sp;
            h.Execute(0x21, 0x78, 0x56, 0x34, 0x12);
            Check.Equal(before - 4, h.Vm.Cpu.Sp);
        });

        r.Add(g, "PUSH8 decrements SP by one", () => {
            VmHarness h = new();
            uint before = h.Vm.Cpu.Sp;
            h.Execute(0x25, 0x78);
            Check.Equal(before - 1, h.Vm.Cpu.Sp);
        });

        r.Add(g, "PUSH then POP restores SP", () => {
            VmHarness h = new();
            uint before = h.Vm.Cpu.Sp;
            h.Vm.LoadData([0x21, 0xEF, 0xBE, 0xAD, 0xDE, 0x26, 0x01]);
            h.Vm.Cpu.Ip = 0;
            h.Vm.ExecuteInstruction();
            h.Vm.ExecuteInstruction();
            Check.Equal(before, h.Vm.Cpu.Sp);
        });

        r.Add(g, "CALL pushes return address and jumps", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0;
            h.Execute(0x3f, 0x01, 0x00, 0x10, 0x00, 0x00);
            Check.Equal(0x1000u, h.Vm.Cpu.Ip);
            Check.Equal(6u, h.Vm.StackPop());
        });

        r.Add(g, "RET pops IP", () => {
            VmHarness h = new();
            h.Vm.StackPush(0x12345678u);
            h.Execute(0x40);
            Check.Equal(0x12345678u, h.Vm.Cpu.Ip);
        });

        r.Add(g, "CALL with base adds base and offset", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 0x100;
            h.Execute(0x3f, 0x01, 0x20, 0x00, 0x00, 0x00);
            Check.Equal(0x120u, h.Vm.Cpu.Ip);
        });

        r.Add(g, "PUSH Fl preserves value", () => {
            VmHarness h = new();
            h.Vm.Cpu.Fl = 0x0F;
            h.Execute(0x20, 0x0A);
            Check.Equal(0x0Fu, h.Vm.StackPop());
        });
    }
}
