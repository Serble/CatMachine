namespace CatVM.AotTest.Tests;

/// <summary>ADD, SUB, UMUL/IMUL, UDIV/IDIV with flag and remainder semantics.</summary>
public static class ArithmeticTests {
    public static void Register(TestRunner r) {
        const string g = "Arithmetic";

        r.Add(g, "ADD R,R", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 5; h.Vm.Cpu.R5 = 10;
            h.Execute(0x14, 0x04, 0x05);
            Check.Equal(15u, h.Vm.Cpu.R4);
        });

        r.Add(g, "ADD R,I", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 5;
            h.Execute(0x15, 0x04, 0x0A, 0x00, 0x00, 0x00);
            Check.Equal(15u, h.Vm.Cpu.R4);
        });

        r.Add(g, "ADD sets zero flag", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 0; h.Vm.Cpu.R5 = 0;
            h.Execute(0x14, 0x04, 0x05);
            Check.True(h.Vm.Cpu.ZeroFlag);
            Check.False(h.Vm.Cpu.CarryFlag);
        });

        r.Add(g, "ADD sets carry on unsigned wrap", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 0xFFFFFFFF; h.Vm.Cpu.R5 = 1;
            h.Execute(0x14, 0x04, 0x05);
            Check.Equal(0u, h.Vm.Cpu.R4);
            Check.True(h.Vm.Cpu.CarryFlag);
            Check.True(h.Vm.Cpu.ZeroFlag);
        });

        r.Add(g, "ADD sets overflow on signed wrap", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 0x7FFFFFFF;
            h.Execute(0x15, 0x04, 0x01, 0x00, 0x00, 0x00);
            Check.Equal(0x80000000u, h.Vm.Cpu.R4);
            Check.True(h.Vm.Cpu.OverflowFlag);
            Check.True(h.Vm.Cpu.SignFlag);
            Check.False(h.Vm.Cpu.CarryFlag);
        });

        r.Add(g, "SUB R,R", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 10; h.Vm.Cpu.R5 = 3;
            h.Execute(0x16, 0x04, 0x05);
            Check.Equal(7u, h.Vm.Cpu.R4);
        });

        r.Add(g, "SUB equal operands sets zero flag", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 5; h.Vm.Cpu.R5 = 5;
            h.Execute(0x16, 0x04, 0x05);
            Check.Equal(0u, h.Vm.Cpu.R4);
            Check.True(h.Vm.Cpu.ZeroFlag);
        });

        r.Add(g, "UMUL R,R", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = 6; h.Vm.Cpu.R2 = 7;
            h.Execute(0x18, 0x01, 0x02);
            Check.Equal(42u, h.Vm.Cpu.R1);
        });

        r.Add(g, "IMUL R,I negative", () => {
            VmHarness h = new();
            h.Vm.Cpu.R1 = unchecked((uint)-3);
            h.Execute(0x1B, 0x01, 0x0A, 0x00, 0x00, 0x00);
            Check.Equal(unchecked((uint)-30), h.Vm.Cpu.R1);
        });

        r.Add(g, "UDIV quotient and remainder", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 17; h.Vm.Cpu.R5 = 5;
            h.Execute(0x1c, 0x04, 0x05);
            Check.Equal(3u, h.Vm.Cpu.R4);
            Check.Equal(2u, h.Vm.Cpu.R5);
        });

        r.Add(g, "IDIV negative dividend", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = unchecked((uint)-17); h.Vm.Cpu.R5 = 5;
            h.Execute(0x1d, 0x04, 0x05);
            Check.Equal(unchecked((uint)-3), h.Vm.Cpu.R4);
            Check.Equal(unchecked((uint)-2), h.Vm.Cpu.R5);
        });

        r.Add(g, "UDIV by zero raises divide-by-zero fault", () => {
            VmHarness h = new();
            h.Vm.Cpu.R4 = 10; h.Vm.Cpu.R5 = 0;
            h.Vm.LoadData([0x1c, 0x04, 0x05]);
            h.Vm.Cpu.Ip = 0;
            h.Vm.ExecuteWithErrorHandling(() => h.Vm.ExecuteInstruction(fast: true));
            Check.True(h.Vm.Paused);
        });
    }
}
