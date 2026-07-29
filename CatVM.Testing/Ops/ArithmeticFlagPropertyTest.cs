namespace CatVM.Testing.Ops;

/// <summary>
/// Cross-checks the branchless composite-Fl writes in Add/Sub/Cmp against a straightforward
/// reference flag computation. Edge values plus a fixed-seed random sweep — gives high
/// confidence that the bit-tricks in the hot path are equivalent to the original setter form.
/// </summary>
public class ArithmeticFlagPropertyTest : OperationTestBase {

    private static readonly uint[] EdgeValues = [
        0u, 1u, 2u,
        0x7FFFFFFEu, 0x7FFFFFFFu,            // INT_MAX-1, INT_MAX
        0x80000000u, 0x80000001u,            // INT_MIN, INT_MIN+1
        0xFFFFFFFEu, 0xFFFFFFFFu,            // -2, -1
        0x12345678u, 0xDEADBEEFu,
    ];

    private static (bool z, bool c, bool s, bool o) ReferenceAddFlags(uint a, uint b) {
        uint result = a + b;
        bool z = result == 0;
        bool c = result < a || result < b;
        bool s = (int)result < 0;
        bool o = (~(a ^ b) & (a ^ result)) >> 31 == 1;
        return (z, c, s, o);
    }

    private static (bool z, bool c, bool s, bool o) ReferenceSubFlags(uint a, uint b) {
        uint result = a - b;
        bool z = result == 0;
        bool c = a < b;
        bool s = (int)result < 0;
        bool o = ((a ^ b) & (a ^ result)) >> 31 == 1;
        return (z, c, s, o);
    }

    private void Step(params byte[] data) {
        // Bypass OperationTestBase.Execute so we can run thousands of instructions per test
        // without TicksPassed accumulating into a real Thread.Sleep on the (cyclesPerSecond=10_000)
        // VM. fast=true skips all timing logic.
        _vm.LoadData(data);
        _vm.Cpu.Ip = 0;
        _vm.ExecuteInstruction(true);
    }

    private void RunAdd(uint a, uint b) {
        _vm.Cpu.R0 = a;
        _vm.Cpu.R1 = b;
        // Set Fl to non-zero pattern to make sure the upper 28 bits are preserved
        // and the low 4 bits are overwritten cleanly.
        _vm.Cpu.Fl = 0xAAAAAAA0u;
        Step(0x14, 0x00, 0x01); // ADD R0, R1
        var (z, c, s, o) = ReferenceAddFlags(a, b);
        uint expectedHigh = 0xAAAAAAA0u & 0xFFFFFFF0u;
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R0, Is.EqualTo(unchecked(a + b)), $"ADD result a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.ZeroFlag,     Is.EqualTo(z), $"ADD Z a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.CarryFlag,    Is.EqualTo(c), $"ADD C a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.SignFlag,     Is.EqualTo(s), $"ADD S a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.OverflowFlag, Is.EqualTo(o), $"ADD O a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.Fl & 0xFFFFFFF0u, Is.EqualTo(expectedHigh), "ADD upper Fl bits clobbered");
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u), $"ADD IP a=0x{a:X8} b=0x{b:X8}");
        });
    }

    private void RunSub(uint a, uint b) {
        _vm.Cpu.R0 = a;
        _vm.Cpu.R1 = b;
        _vm.Cpu.Fl = 0xAAAAAAA0u;
        Step(0x16, 0x00, 0x01); // SUB R0, R1
        var (z, c, s, o) = ReferenceSubFlags(a, b);
        uint expectedHigh = 0xAAAAAAA0u & 0xFFFFFFF0u;
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R0, Is.EqualTo(unchecked(a - b)), $"SUB result a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.ZeroFlag,     Is.EqualTo(z), $"SUB Z a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.CarryFlag,    Is.EqualTo(c), $"SUB C a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.SignFlag,     Is.EqualTo(s), $"SUB S a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.OverflowFlag, Is.EqualTo(o), $"SUB O a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.Fl & 0xFFFFFFF0u, Is.EqualTo(expectedHigh), "SUB upper Fl bits clobbered");
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u), $"SUB IP a=0x{a:X8} b=0x{b:X8}");
        });
    }

    private void RunCmp(uint a, uint b) {
        _vm.Cpu.R0 = a;
        _vm.Cpu.R1 = b;
        _vm.Cpu.Fl = 0xAAAAAAA0u;
        Step(0x31, 0x00, 0x01); // CMP RR
        var (z, c, s, o) = ReferenceSubFlags(a, b);
        uint expectedHigh = 0xAAAAAAA0u & 0xFFFFFFF0u;
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R0, Is.EqualTo(a), "CMP must not modify destination register");
            Assert.That(_vm.Cpu.ZeroFlag,     Is.EqualTo(z), $"CMP Z a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.CarryFlag,    Is.EqualTo(c), $"CMP C a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.SignFlag,     Is.EqualTo(s), $"CMP S a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.OverflowFlag, Is.EqualTo(o), $"CMP O a=0x{a:X8} b=0x{b:X8}");
            Assert.That(_vm.Cpu.Fl & 0xFFFFFFF0u, Is.EqualTo(expectedHigh), "CMP upper Fl bits clobbered");
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u), $"CMP IP a=0x{a:X8} b=0x{b:X8}");
        });
    }

    [Test]
    public void AddFlagsMatchReferenceOnEdgeValues() {
        foreach (uint a in EdgeValues)
        foreach (uint b in EdgeValues)
            RunAdd(a, b);
    }

    [Test]
    public void SubFlagsMatchReferenceOnEdgeValues() {
        foreach (uint a in EdgeValues)
        foreach (uint b in EdgeValues)
            RunSub(a, b);
    }

    [Test]
    public void CmpFlagsMatchReferenceOnEdgeValues() {
        foreach (uint a in EdgeValues)
        foreach (uint b in EdgeValues)
            RunCmp(a, b);
    }

    [Test]
    public void AddFlagsMatchReferenceOnRandomInputs() {
        Random rng = new(0xCA7);
        for (int i = 0; i < 2048; i++) {
            RunAdd((uint)rng.NextInt64(0, uint.MaxValue), (uint)rng.NextInt64(0, uint.MaxValue));
        }
    }

    [Test]
    public void SubFlagsMatchReferenceOnRandomInputs() {
        Random rng = new(0xCA7);
        for (int i = 0; i < 2048; i++) {
            RunSub((uint)rng.NextInt64(0, uint.MaxValue), (uint)rng.NextInt64(0, uint.MaxValue));
        }
    }

    [Test]
    public void CmpFlagsMatchReferenceOnRandomInputs() {
        Random rng = new(0xCA7);
        for (int i = 0; i < 2048; i++) {
            RunCmp((uint)rng.NextInt64(0, uint.MaxValue), (uint)rng.NextInt64(0, uint.MaxValue));
        }
    }
}
