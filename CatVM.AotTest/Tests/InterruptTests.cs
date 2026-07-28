namespace CatVM.AotTest.Tests;

/// <summary>INT dispatch through the interrupt table, DI/EI and privilege gating.</summary>
public static class InterruptTests {
    private const byte OpIntR = 0x1E;
    private const byte OpIntI = 0x1F;
    private const byte OpDi = 0x45;
    private const byte OpEi = 0x46;
    private const byte OpNop = 0x4D;

    private static byte[] MakeIt(byte id, uint handlerAddr) => [
        1, id,
        (byte)(handlerAddr & 0xFF),
        (byte)((handlerAddr >> 8) & 0xFF),
        (byte)((handlerAddr >> 16) & 0xFF),
        (byte)((handlerAddr >> 24) & 0xFF),
    ];

    public static void Register(TestRunner r) {
        const string g = "Interrupts";

        r.Add(g, "INT I dispatches into IT handler", () => {
            VmHarness h = new();
            const uint handlerAddr = 0x80;
            h.Vm.LoadData([OpIntI, 0x42]);
            h.Vm.LoadData([OpNop], handlerAddr);
            h.Vm.LoadData(MakeIt(0x42, handlerAddr), 0x100);
            h.Vm.Cpu.It = 0x100;
            h.Vm.Cpu.Ip = 0;
            h.Vm.ExecuteInstruction();
            Check.Equal(handlerAddr, h.Vm.Cpu.Ip);
        });

        r.Add(g, "INT R uses low byte as vector", () => {
            VmHarness h = new();
            const uint handlerAddr = 0x80;
            h.Vm.LoadData([OpIntR, 0x01]);
            h.Vm.LoadData([OpNop], handlerAddr);
            h.Vm.LoadData(MakeIt(0x42, handlerAddr), 0x100);
            h.Vm.Cpu.It = 0x100;
            h.Vm.Cpu.Ip = 0;
            h.Vm.Cpu.R1 = 0xDEAD_BE42;
            h.Vm.ExecuteInstruction();
            Check.Equal(handlerAddr, h.Vm.Cpu.Ip);
        });

        r.Add(g, "DI disables interrupts", () => {
            VmHarness h = new();
            h.Vm.InterruptsEnabled = true;
            h.Execute(OpDi);
            Check.False(h.Vm.InterruptsEnabled);
        });

        r.Add(g, "EI enables interrupts", () => {
            VmHarness h = new();
            h.Vm.InterruptsEnabled = false;
            h.Execute(OpEi);
            Check.True(h.Vm.InterruptsEnabled);
        });

        r.Add(g, "INT I in user mode faults", () => {
            VmHarness h = new();
            h.Vm.LoadData([OpIntI, 0x42], 0x100);
            h.Vm.Cpu.MBase = 0x100;
            h.Vm.Cpu.MLen = 0x100;
            h.Vm.Cpu.Sp = 0x100;
            h.Vm.Cpu.Ip = 0;
            h.Vm.Cpu.Mode = 0b01;
            h.Vm.Cpu.It = uint.MaxValue;
            h.Vm.ExecuteInstruction();
            Check.True(h.Vm.Paused);
        });

        r.Add(g, "DI in user mode faults and stays enabled", () => {
            VmHarness h = new();
            h.Vm.LoadData([OpDi], 0x100);
            h.Vm.Cpu.MBase = 0x100;
            h.Vm.Cpu.MLen = 0x100;
            h.Vm.Cpu.Sp = 0x100;
            h.Vm.Cpu.Ip = 0;
            h.Vm.Cpu.Mode = 0b01;
            h.Vm.Cpu.It = uint.MaxValue;
            h.Vm.InterruptsEnabled = true;
            h.Vm.ExecuteInstruction();
            Check.True(h.Vm.Paused);
            Check.True(h.Vm.InterruptsEnabled);
        });

        r.Add(g, "DI in driver mode is allowed", () => {
            VmHarness h = new();
            h.Vm.LoadData([OpDi], 0x100);
            h.Vm.Cpu.MBase = 0x100;
            h.Vm.Cpu.MLen = 0x100;
            h.Vm.Cpu.Sp = 0x100;
            h.Vm.Cpu.Ip = 0;
            h.Vm.Cpu.Mode = 0b11;
            h.Vm.InterruptsEnabled = true;
            h.Vm.ExecuteInstruction();
            Check.False(h.Vm.Paused);
            Check.False(h.Vm.InterruptsEnabled);
        });
    }
}
