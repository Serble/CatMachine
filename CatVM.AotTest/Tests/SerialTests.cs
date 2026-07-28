using CatVM.Serial;

namespace CatVM.AotTest.Tests;

/// <summary>IN/OUT serial opcodes plus supervisor-mode privilege gating.</summary>
public static class SerialTests {
    private static (VmHarness h, List<uint> output, Queue<uint> input) NewSerialVm() {
        VmHarness h = new();
        List<uint> output = [];
        Queue<uint> input = new();
        h.Vm.RegisterSerialDevice(18, new SerialDevice(18,
            _ => input.Dequeue(),
            (_, val) => output.Add(val)));
        return (h, output, input);
    }

    public static void Register(TestRunner r) {
        const string g = "Serial IO";

        r.Add(g, "IN R,R reads from device", () => {
            (VmHarness h, _, Queue<uint> input) = NewSerialVm();
            input.Enqueue(123);
            h.Vm.Cpu.R2 = 18;
            h.Execute(0x47, 0x01, 0x02);
            Check.Equal(123u, h.Vm.Cpu.R1);
        });

        r.Add(g, "IN R,I reads from device", () => {
            (VmHarness h, _, Queue<uint> input) = NewSerialVm();
            input.Enqueue(123);
            h.Execute(0x48, 0x01, 18, 0x00, 0x00, 0x00);
            Check.Equal(123u, h.Vm.Cpu.R1);
        });

        r.Add(g, "OUT R,R writes to device", () => {
            (VmHarness h, List<uint> output, _) = NewSerialVm();
            h.Vm.Cpu.R1 = 18; h.Vm.Cpu.R2 = 123;
            h.Execute(0x49, 0x01, 0x02);
            Check.SequenceEqual([123u], output);
        });

        r.Add(g, "OUT I,I writes immediate", () => {
            (VmHarness h, List<uint> output, _) = NewSerialVm();
            h.Execute(0x4c, 18, 0x00, 0x00, 0x00, 123, 0x00, 0x00, 0x00);
            Check.SequenceEqual([123u], output);
        });

        r.Add(g, "OUT in user mode faults and does not emit", () => {
            (VmHarness h, List<uint> output, _) = NewSerialVm();
            h.Vm.Cpu.R1 = 18; h.Vm.Cpu.R2 = 123;
            h.Vm.LoadData([0x49, 0x01, 0x02], 0x100);
            h.Vm.Cpu.MBase = 0x100;
            h.Vm.Cpu.MLen = 0x100;
            h.Vm.Cpu.Sp = 0x100;
            h.Vm.Cpu.Mode = 0b01;
            h.Vm.Cpu.It = uint.MaxValue;
            h.Vm.Cpu.Ip = 0;
            h.Vm.ExecuteInstruction();
            Check.True(h.Vm.Paused);
            Check.Equal(0, output.Count);
        });

        r.Add(g, "OUT in driver mode is allowed", () => {
            (VmHarness h, List<uint> output, _) = NewSerialVm();
            h.Vm.Cpu.R1 = 18;
            h.Vm.LoadData([0x4a, 0x01, 123, 0x00, 0x00, 0x00], 0x100);
            h.Vm.Cpu.MBase = 0x100;
            h.Vm.Cpu.MLen = 0x100;
            h.Vm.Cpu.Sp = 0x100;
            h.Vm.Cpu.Mode = 0b11;
            h.Vm.Cpu.It = uint.MaxValue;
            h.Vm.Cpu.Ip = 0;
            h.Vm.ExecuteInstruction();
            Check.False(h.Vm.Paused);
            Check.SequenceEqual([123u], output);
        });
    }
}
