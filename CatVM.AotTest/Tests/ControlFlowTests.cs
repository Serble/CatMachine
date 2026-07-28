namespace CatVM.AotTest.Tests;

/// <summary>CMP flag setting and conditional/unconditional jumps.</summary>
public static class ControlFlowTests {
    public static void Register(TestRunner r) {
        const string g = "Control Flow";

        r.Add(g, "CMP equal sets zero flag", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 5; h.Vm.Cpu.R5 = 5;
            h.Execute(0x31, 0x04, 0x05);
            Check.True(h.Vm.Cpu.ZeroFlag);
            Check.False(h.Vm.Cpu.CarryFlag);
        });

        r.Add(g, "CMP less sets carry and sign", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 5; h.Vm.Cpu.R5 = 10;
            h.Execute(0x31, 0x04, 0x05);
            Check.False(h.Vm.Cpu.ZeroFlag);
            Check.True(h.Vm.Cpu.CarryFlag);
            Check.True(h.Vm.Cpu.SignFlag);
        });

        r.Add(g, "CMP does not mutate registers", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 10; h.Vm.Cpu.R5 = 5;
            h.Execute(0x31, 0x04, 0x05);
            Check.Equal(10u, h.Vm.Cpu.R4);
            Check.Equal(5u, h.Vm.Cpu.R5);
        });

        r.Add(g, "JMP absolute with base register", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 5;
            h.Execute(0x30, 0x04, 0x12, 0x00, 0x00, 0x00);
            Check.Equal(0x17u, h.Vm.Cpu.Ip);
        });

        r.Add(g, "JMP absolute addrReg 0xFF ignores base", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 0xDEAD;
            h.Execute(0x30, 0xFF, 0x12, 0x00, 0x00, 0x00);
            Check.Equal(0x12u, h.Vm.Cpu.Ip);
        });

        r.Add(g, "JZ taken when zero flag set", () => {
            VmHarness h = new();
            h.Vm.Cpu.ZeroFlag = true;
            h.Vm.Cpu.R4 = 5;
            h.Execute(0x35, 0x04, 0x12, 0x00, 0x00, 0x00);
            Check.Equal(0x17u, h.Vm.Cpu.Ip);
        });

        r.Add(g, "JZ not taken falls through", () => {
            VmHarness h = new();
            h.Vm.Cpu.ZeroFlag = false;
            h.Vm.Cpu.R4 = 5;
            h.Execute(0x35, 0x04, 0x12, 0x00, 0x00, 0x00);
            Check.Equal(0x06u, h.Vm.Cpu.Ip);
        });

        r.Add(g, "JNZ taken when zero flag clear", () => {
            VmHarness h = new();
            h.Vm.Cpu.ZeroFlag = false;
            h.Vm.Cpu.R4 = 5;
            h.Execute(0x36, 0x04, 0x12, 0x00, 0x00, 0x00);
            Check.Equal(0x17u, h.Vm.Cpu.Ip);
        });

        r.Add(g, "CMP then JUG integration jumps", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 10; h.Vm.Cpu.R5 = 5; h.Vm.Cpu.R6 = 0;
            h.Vm.LoadData([
                0x31, 0x04, 0x05,
                0x39, 0x06, 0x10, 0x00, 0x00, 0x00,
            ]);
            h.Vm.Cpu.Ip = 0;
            h.Vm.ExecuteInstruction();
            h.Vm.ExecuteInstruction();
            Check.Equal(0x10u, h.Vm.Cpu.Ip);
        });

        r.Add(g, "CMP then JIL signed-less-than jumps", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 0xFFFFFFFF; h.Vm.Cpu.R5 = 1; h.Vm.Cpu.R6 = 0;
            h.Vm.LoadData([
                0x31, 0x04, 0x05,
                0x3b, 0x06, 0x10, 0x00, 0x00, 0x00,
            ]);
            h.Vm.Cpu.Ip = 0;
            h.Vm.ExecuteInstruction();
            h.Vm.ExecuteInstruction();
            Check.Equal(0x10u, h.Vm.Cpu.Ip);
        });
    }
}
