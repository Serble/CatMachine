using CatVM.Serial;

namespace CatVM.AotTest.Tests;

/// <summary>Lifecycle (Reset/LoadData/ctor), serial registry, physical reads,
/// error handling, hardware-interrupt queue and timing bookkeeping.</summary>
public static class CoreTests {
    private static CatVm NewVm(int mem = 256, byte[]? rom = null) =>
        new(mem, 10_000, rom) { Fast = true };

    public static void Register(TestRunner r) {
        const string g = "CatVm Core";

        r.Add(g, "Reset clears CPU and memory", () => {
            CatVm vm = NewVm();
            vm.Cpu.R0 = 0xAB;
            vm.Memory[0] = 0xFF;
            vm.Reset();
            Check.Equal(0u, vm.Cpu.R0);
            Check.Equal((byte)0, vm.Memory[0]);
            Check.Equal(256u, vm.Cpu.Sp);
            Check.Equal(256u, vm.Cpu.MLen);
        });

        r.Add(g, "Reset preserveMem keeps memory but resets CPU", () => {
            CatVm vm = NewVm();
            vm.Memory[5] = 0xDE;
            vm.Cpu.R0 = 0x42;
            vm.Reset(preserveMem: true);
            Check.Equal(0u, vm.Cpu.R0);
            Check.Equal((byte)0xDE, vm.Memory[5]);
        });

        r.Add(g, "Reset reloads ROM", () => {
            CatVm vm = NewVm(rom: [0xAA, 0xBB]);
            vm.Memory[0] = 0xFF;
            vm.Reset();
            Check.Equal((byte)0xAA, vm.Memory[0]);
            Check.Equal((byte)0xBB, vm.Memory[1]);
        });

        r.Add(g, "Ctor throws when ROM larger than memory", () => {
            Check.Throws<Exception>(() => new CatVm(2, 10_000, [1, 2, 3, 4]));
        });

        r.Add(g, "LoadData beyond memory throws", () => {
            CatVm vm = NewVm(mem: 16);
            Check.Throws<Exception>(() => vm.LoadData(new byte[8], address: 12));
        });

        r.Add(g, "LoadData exact fit succeeds", () => {
            CatVm vm = NewVm(mem: 16);
            vm.LoadData([1, 2, 3, 4, 5, 6, 7, 8], 8);
            Check.Equal((byte)8, vm.Memory[15]);
        });

        r.Add(g, "RegisterSerialDevice duplicate port throws", () => {
            CatVm vm = NewVm();
            vm.RegisterSerialDevice(20, ISerialDevice.Null);
            Check.Throws<Exception>(() => vm.RegisterSerialDevice(20, ISerialDevice.Null));
        });

        r.Add(g, "RegisterSerialDevice auto port picks first free", () => {
            CatVm vm = NewVm();
            vm.RegisterSerialDevice(0, ISerialDevice.Null);
            vm.RegisterSerialDevice(2, ISerialDevice.Null);
            vm.RegisterSerialDevice(ISerialDevice.Null);
            Check.True(vm.SerialDevices.ContainsKey(1));
        });

        r.Add(g, "GetSerialDevice unregistered returns null device", () => {
            CatVm vm = NewVm();
            ISerialDevice d = vm.GetSerialDevice(0xFE);
            Check.Equal(uint.MaxValue, d.Type);
        });

        r.Add(g, "Read8Physical bypasses virtual-mode translation", () => {
            CatVm vm = NewVm(mem: 256);
            vm.Memory[0x10] = 0xAB;
            vm.Cpu.MBase = 0x80;
            vm.Cpu.MLen = 0x10;
            vm.Cpu.Mode = 0b01;
            Check.Equal((byte)0xAB, vm.Read8Physical(0x10));
        });

        r.Add(g, "ReadWordPhysical bypasses translation", () => {
            CatVm vm = NewVm(mem: 256);
            BitConverter.GetBytes(0xCAFEBABEu).CopyTo(vm.Memory, 0x20);
            vm.Cpu.MBase = 0x80;
            vm.Cpu.MLen = 0x10;
            vm.Cpu.Mode = 0b01;
            Check.Equal(0xCAFEBABEu, vm.ReadWordPhysical(0x20));
        });

        r.Add(g, "ErrorHandling DivideByZero halts", () => {
            CatVm vm = NewVm();
            vm.ExecuteWithErrorHandling(() => throw new DivideByZeroException());
            Check.True(vm.Paused);
        });

        r.Add(g, "ErrorHandling MemoryOutOfRange halts", () => {
            CatVm vm = NewVm();
            vm.ExecuteWithErrorHandling(() => throw new MemoryOutOfRange(false, 0, 1));
            Check.True(vm.Paused);
        });

        r.Add(g, "ErrorHandling generic exception halts", () => {
            CatVm vm = NewVm();
            vm.ExecuteWithErrorHandling(() => throw new InvalidOperationException("boom"));
            Check.True(vm.Paused);
        });

        r.Add(g, "ErrorHandling ArgumentException halts", () => {
            CatVm vm = NewVm();
            vm.ExecuteWithErrorHandling(() => throw new ArgumentException("bad arg"));
            Check.True(vm.Paused);
        });

        r.Add(g, "ErrorHandling DumpErrors writes to console", () => {
            CatVm vm = new(64, 10_000) { Fast = true, DumpErrors = true };
            TextWriter old = Console.Out;
            StringWriter w = new();
            Console.SetOut(w);
            try {
                vm.ExecuteWithErrorHandling(() => throw new InvalidOperationException("dump-me"));
            }
            finally {
                Console.SetOut(old);
            }
            Check.True(w.ToString().Contains("dump-me"));
        });

        r.Add(g, "HandleInterrupt 0x90 gated by EnableTestingInterrupts", () => {
            TextWriter old = Console.Out;
            StringWriter w = new();
            Console.SetOut(w);
            try {
                CatVm disabled = NewVm();
                disabled.Cpu.R1 = 0x42;
                disabled.HandleInterrupt(0x90);
                string disabledOut = w.ToString();

                CatVm enabled = new(256, 10_000) { Fast = true, EnableTestingInterrupts = true };
                enabled.Cpu.R1 = 0x42;
                w.GetStringBuilder().Clear();
                enabled.HandleInterrupt(0x90);
                string enabledOut = w.ToString();

                Check.False(disabledOut.Contains("66"), "0x90 ignored when disabled");
                Check.True(enabledOut.Contains("66"), "0x90 prints when enabled");
            }
            finally {
                Console.SetOut(old);
            }
        });

        r.Add(g, "HandleInterrupt unknown code is a no-op", () => {
            CatVm vm = NewVm();
            vm.HandleInterrupt(0x42);
            Check.False(vm.Paused);
        });

        r.Add(g, "HardwareInterrupt delivered on next execute", () => {
            CatVm vm = NewVm();
            vm.LoadData([0x4D, 0x4D]);
            vm.HardwareInterrupt((byte)SpecialInterrupts.ProtectionFault);
            vm.ExecuteInstruction(true);
            Check.True(vm.Paused);
        });

        r.Add(g, "HardwareInterrupt not delivered when disabled", () => {
            CatVm vm = NewVm();
            vm.InterruptsEnabled = false;
            vm.LoadData([0x4D]);
            vm.HardwareInterrupt((byte)SpecialInterrupts.ProtectionFault);
            vm.ExecuteInstruction(true);
            Check.False(vm.Paused);
        });

        r.Add(g, "CyclesPerSecond round-trips", () => {
            CatVm vm = NewVm();
            vm.CyclesPerSecond = 50_000;
            Check.Equal(50_000u, vm.CyclesPerSecond);
        });

        r.Add(g, "Paused stops and resumes runtime stopwatch", () => {
            CatVm vm = NewVm();
            vm.Paused = true;
            Check.False(vm.Runtime.IsRunning);
            vm.Paused = false;
            Check.True(vm.Runtime.IsRunning);
        });
    }
}
