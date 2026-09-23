namespace CatVM.Testing.Ops;

/// <summary>
/// Cross-cutting checks for every opcode that writes a register:
/// <list type="bullet">
///   <item>Writing IP is a jump, the instruction's own IP advance must not be added on top.</item>
///   <item>An invalid destination register faults with IP and SP untouched.</item>
/// </list>
/// MOV and IN are covered in their own fixtures.
/// </summary>
public class RegisterDestinationTest : OperationTestBase {
    private const uint Start = 0x150;
    private const byte Ip = 0x09;
    private const byte R1 = 0x01;
    private const byte BadReg = 0x20;

    private static byte Op(string name) => (byte)Array.IndexOf(CatVm.OperationNames, name);

    private static TestCaseData IntoIp(string name, byte[] operands, uint expectedIp, Action<CatVm>? setup = null) =>
        new TestCaseData((byte[])[Op(name), ..operands], setup, expectedIp)
            .SetName($"{name}_IntoIp_LeavesIpAtResult");

    // Start = 0x150, so e.g. ADD IP, 0x20 must land on 0x170, not 0x173.
    private static IEnumerable<TestCaseData> IntoIpCases() {
        yield return IntoIp("AddRR", [Ip, R1], 0x170, vm => vm.Cpu.R1 = 0x20);
        yield return IntoIp("AddRI", [Ip, 0x20, 0x00, 0x00, 0x00], 0x170);
        yield return IntoIp("SubRR", [Ip, R1], 0x130, vm => vm.Cpu.R1 = 0x20);
        yield return IntoIp("SubRI", [Ip, 0x20, 0x00, 0x00, 0x00], 0x130);
        yield return IntoIp("MulRR", [Ip, R1], 0x2A0, vm => vm.Cpu.R1 = 2);
        yield return IntoIp("MulRI", [Ip, 0x02, 0x00, 0x00, 0x00], 0x2A0);
        yield return IntoIp("IMulRR", [Ip, R1], 0x2A0, vm => vm.Cpu.R1 = 2);
        yield return IntoIp("IMulRI", [Ip, 0x02, 0x00, 0x00, 0x00], 0x2A0);
        yield return IntoIp("OrRR", [Ip, R1], 0x15F, vm => vm.Cpu.R1 = 0x0F);
        yield return IntoIp("OrRI", [Ip, 0x0F, 0x00, 0x00, 0x00], 0x15F);
        yield return IntoIp("AndRR", [Ip, R1], 0x050, vm => vm.Cpu.R1 = 0xF0);
        yield return IntoIp("AndRI", [Ip, 0xF0, 0x00, 0x00, 0x00], 0x050);
        yield return IntoIp("XorRR", [Ip, R1], 0x1A0, vm => vm.Cpu.R1 = 0xF0);
        yield return IntoIp("XorRI", [Ip, 0xF0, 0x00, 0x00, 0x00], 0x1A0);
        yield return IntoIp("NotR", [Ip], ~Start);
        yield return IntoIp("ShlRR", [Ip, R1], 0x2A0, vm => vm.Cpu.R1 = 1);
        yield return IntoIp("ShlRI", [Ip, 0x01, 0x00, 0x00, 0x00], 0x2A0);
        yield return IntoIp("ShrRR", [Ip, R1], 0x015, vm => vm.Cpu.R1 = 4);
        yield return IntoIp("ShrRI", [Ip, 0x04, 0x00, 0x00, 0x00], 0x015);
        yield return IntoIp("PopR", [Ip], 0x42, vm => vm.StackPush(0x42u));
        yield return IntoIp("Pop16R", [Ip], 0x42, vm => vm.StackPush((ushort)0x42));
        yield return IntoIp("Pop8R", [Ip], 0x42, vm => vm.StackPush((byte)0x42));
        yield return IntoIp("GetItR", [Ip], 0x42, vm => vm.Cpu.It = 0x42);
        yield return IntoIp("GetKspR", [Ip], 0x42, vm => vm.Cpu.Ksp = 0x42);

        // DIV writes both operands, IP can be either the quotient or the remainder register
        yield return IntoIp("DivRR", [Ip, R1], 0x15, vm => vm.Cpu.R1 = 0x10)
            .SetName("DivRR_QuotientIntoIp_LeavesIpAtResult");
        yield return IntoIp("DivRR", [R1, Ip], 5, vm => vm.Cpu.R1 = 0x155)  // 0x155 % 0x150
            .SetName("DivRR_RemainderIntoIp_LeavesIpAtResult");
        yield return IntoIp("IDivRR", [Ip, R1], 0x15, vm => vm.Cpu.R1 = 0x10)
            .SetName("IDivRR_QuotientIntoIp_LeavesIpAtResult");
        yield return IntoIp("IDivRR", [R1, Ip], 5, vm => vm.Cpu.R1 = 0x155)
            .SetName("IDivRR_RemainderIntoIp_LeavesIpAtResult");
    }

    [TestCaseSource(nameof(IntoIpCases))]
    public void IntoIp_LeavesIpAtResult(byte[] program, Action<CatVm>? setup, uint expectedIp) {
        _vm.LoadData(program, Start);
        _vm.Cpu.Ip = Start;
        setup?.Invoke(_vm);

        _vm.ExecuteInstruction(fast: true);

        Assert.That(_vm.Cpu.Ip, Is.EqualTo(expectedIp));
    }

    private static TestCaseData Invalid(string name, byte[] operands, Action<CatVm>? setup = null) =>
        new TestCaseData((byte[])[Op(name), ..operands], setup)
            .SetName($"{name}_InvalidDestReg_FaultsWithIpAndSpUntouched");

    private static IEnumerable<TestCaseData> InvalidDestCases() {
        yield return Invalid("AddRR", [BadReg, R1]);
        yield return Invalid("AddRI", [BadReg, 0x01, 0x00, 0x00, 0x00]);
        yield return Invalid("SubRR", [BadReg, R1]);
        yield return Invalid("SubRI", [BadReg, 0x01, 0x00, 0x00, 0x00]);
        yield return Invalid("MulRR", [BadReg, R1]);
        yield return Invalid("MulRI", [BadReg, 0x01, 0x00, 0x00, 0x00]);
        yield return Invalid("IMulRR", [BadReg, R1]);
        yield return Invalid("IMulRI", [BadReg, 0x01, 0x00, 0x00, 0x00]);
        yield return Invalid("OrRR", [BadReg, R1]);
        yield return Invalid("OrRI", [BadReg, 0x01, 0x00, 0x00, 0x00]);
        yield return Invalid("AndRR", [BadReg, R1]);
        yield return Invalid("AndRI", [BadReg, 0x01, 0x00, 0x00, 0x00]);
        yield return Invalid("XorRR", [BadReg, R1]);
        yield return Invalid("XorRI", [BadReg, 0x01, 0x00, 0x00, 0x00]);
        yield return Invalid("NotR", [BadReg]);
        yield return Invalid("ShlRR", [BadReg, R1]);
        yield return Invalid("ShlRI", [BadReg, 0x01, 0x00, 0x00, 0x00]);
        yield return Invalid("ShrRR", [BadReg, R1]);
        yield return Invalid("ShrRI", [BadReg, 0x01, 0x00, 0x00, 0x00]);
        yield return Invalid("PopR", [BadReg], vm => vm.StackPush(0x42u));
        yield return Invalid("Pop16R", [BadReg], vm => vm.StackPush((ushort)0x42));
        yield return Invalid("Pop8R", [BadReg], vm => vm.StackPush((byte)0x42));
        yield return Invalid("GetItR", [BadReg]);
        yield return Invalid("GetKspR", [BadReg]);

        yield return Invalid("DivRR", [BadReg, R1], vm => vm.Cpu.R1 = 1)
            .SetName("DivRR_InvalidQuotientReg_FaultsWithIpAndSpUntouched");
        yield return Invalid("DivRR", [R1, BadReg], vm => vm.Cpu.R1 = 1)
            .SetName("DivRR_InvalidRemainderReg_FaultsWithIpAndSpUntouched");
        yield return Invalid("IDivRR", [BadReg, R1], vm => vm.Cpu.R1 = 1)
            .SetName("IDivRR_InvalidQuotientReg_FaultsWithIpAndSpUntouched");
        yield return Invalid("IDivRR", [R1, BadReg], vm => vm.Cpu.R1 = 1)
            .SetName("IDivRR_InvalidRemainderReg_FaultsWithIpAndSpUntouched");
    }

    [TestCaseSource(nameof(InvalidDestCases))]
    public void InvalidDestReg_FaultsWithIpAndSpUntouched(byte[] program, Action<CatVm>? setup) {
        _vm.LoadData(program, Start);
        _vm.Cpu.Ip = Start;
        setup?.Invoke(_vm);
        uint spBefore = _vm.Cpu.Sp;

        Assert.Throws<ArgumentOutOfRangeException>(() => _vm.ExecuteInstruction(fast: true));
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(Start));
            Assert.That(_vm.Cpu.Sp, Is.EqualTo(spBefore), "Stack must not move on a faulted instruction");
        });
    }
}
