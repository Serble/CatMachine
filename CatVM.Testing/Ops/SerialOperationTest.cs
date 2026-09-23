using CatVM.Serial;

namespace CatVM.Testing.Ops;

public class SerialOperationTest : OperationTestBase {
    private List<uint> _serialOutput = [];
    private Queue<uint> _serialInput = [];
    
    [SetUp]
    public void RegisterSerial() {
        _serialOutput.Clear();
        _serialInput.Clear();
        _vm.RegisterSerialDevice(18, new SerialDevice(18,
            _ => _serialInput.Dequeue(), 
            (_, val) => _serialOutput.Add(val)));
    }
    
    [Test]
    public void TestInRR() {
        _serialInput.Enqueue(123);
        _vm.Cpu.R2 = 18;
        Execute(0x47, 0x01, 0x02);  // IN R1, R2
        Assert.That(_vm.Cpu.R1, Is.EqualTo(123));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestInRI() {
        _serialInput.Enqueue(123);
        Execute(0x48, 0x01, 18, 0x00, 0x00, 0x00);  // IN R1, 18
        Assert.That(_vm.Cpu.R1, Is.EqualTo(123));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestOutRR() {
        _vm.Cpu.R1 = 18;
        _vm.Cpu.R2 = 123;
        _serialOutput.Clear();
        Execute(0x49, 0x01, 0x02);  // OUT R1, R2
        Assert.That(_serialOutput, Is.EqualTo([123]));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }

    [Test]
    public void TestOutRI() {
        _vm.Cpu.R1 = 18;
        _serialOutput.Clear();
        Execute(0x4a, 0x01, 123, 0x00, 0x00, 0x00); // OUT R1, 18
        Assert.That(_serialOutput, Is.EqualTo([123]));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }
    
    [Test]
    public void TestOutIR() {
        _vm.Cpu.R1 = 123;
        _serialOutput.Clear();
        Execute(0x4b, 18, 0x00, 0x00, 0x00, 0x01); // OUT 18, R1
        Assert.That(_serialOutput, Is.EqualTo([123]));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }
    
    [Test]
    public void TestOutII() {
        _serialOutput.Clear();
        Execute(0x4c, 18, 0x00, 0x00, 0x00, 123, 0x00, 0x00, 0x00); // OUT 18, 18
        Assert.That(_serialOutput, Is.EqualTo([123]));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(9u));
    }

    // ---- Device side effects on IP ----
    // A device may move IP itself (e.g. HardwareManager's reset), the instruction's own IP advance
    // must happen before the device runs so it doesn't clobber that.

    private const uint JumpPort = 19;
    private const uint JumpTarget = 0x80;

    private void RegisterJumpDevice() {
        _vm.RegisterSerialDevice(JumpPort, new SerialDevice(JumpPort,
            vm => {
                vm.Cpu.Ip = JumpTarget;
                return 123;
            },
            (vm, _) => vm.Cpu.Ip = JumpTarget));
    }

    private static IEnumerable<TestCaseData> IpMovingInstructions() {
        yield return new TestCaseData(new byte[] { 0x47, 0x01, 0x02 })  // IN R1, R2
            .SetName("InRR_DeviceMovesIp_IsNotClobbered");
        yield return new TestCaseData(new byte[] { 0x48, 0x01, 19, 0x00, 0x00, 0x00 })  // IN R1, 19
            .SetName("InRI_DeviceMovesIp_IsNotClobbered");
        yield return new TestCaseData(new byte[] { 0x49, 0x02, 0x03 })  // OUT R2, R3
            .SetName("OutRR_DeviceMovesIp_IsNotClobbered");
        yield return new TestCaseData(new byte[] { 0x4a, 0x02, 123, 0x00, 0x00, 0x00 })  // OUT R2, 123
            .SetName("OutRI_DeviceMovesIp_IsNotClobbered");
        yield return new TestCaseData(new byte[] { 0x4b, 19, 0x00, 0x00, 0x00, 0x03 })  // OUT 19, R3
            .SetName("OutIR_DeviceMovesIp_IsNotClobbered");
        yield return new TestCaseData(new byte[] { 0x4c, 19, 0x00, 0x00, 0x00, 123, 0x00, 0x00, 0x00 })  // OUT 19, 123
            .SetName("OutII_DeviceMovesIp_IsNotClobbered");
    }

    [TestCaseSource(nameof(IpMovingInstructions))]
    public void Serial_DeviceMovesIp_IsNotClobbered(byte[] program) {
        RegisterJumpDevice();
        _vm.Cpu.R2 = JumpPort;
        _vm.Cpu.R3 = 123;

        Execute(program);

        Assert.That(_vm.Cpu.Ip, Is.EqualTo(JumpTarget));
    }

    [Test]
    public void InRR_DeviceMovesIp_StillWritesDestination() {
        RegisterJumpDevice();
        _vm.Cpu.R2 = JumpPort;

        Execute(0x47, 0x01, 0x02);  // IN R1, R2

        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R1, Is.EqualTo(123u));
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(JumpTarget));
        });
    }

    private static IEnumerable<TestCaseData> InvalidDestInstructions() {
        yield return new TestCaseData(new byte[] { 0x47, 0x20, 0x02 })  // IN <reg 0x20>, R2
            .SetName("InRR_InvalidDestReg_FaultsWithIpAtInstruction");
        yield return new TestCaseData(new byte[] { 0x48, 0x20, 18, 0x00, 0x00, 0x00 })  // IN <reg 0x20>, 18
            .SetName("InRI_InvalidDestReg_FaultsWithIpAtInstruction");
    }

    [TestCaseSource(nameof(InvalidDestInstructions))]
    public void In_InvalidDestReg_FaultsWithIpAtInstruction(byte[] program) {
        _serialInput.Enqueue(123);
        _vm.Cpu.R2 = 18;
        _vm.LoadData(program, 0x20);
        _vm.Cpu.Ip = 0x20;

        Assert.Throws<ArgumentOutOfRangeException>(() => _vm.ExecuteInstruction(fast: true));
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(0x20u));
            Assert.That(_serialInput.Count, Is.EqualTo(1), "Input() must not run for an invalid destination");
        });
    }

    // ---- Privilege gates ----

    private void SetupUserMode() {
        const uint mbase = 0x100;
        const uint mlen  = 0x100;
        _vm.Cpu.MBase = mbase;
        _vm.Cpu.MLen  = mlen;
        _vm.Cpu.Sp    = mlen;
        _vm.Cpu.Mode  = 0b01;
        _vm.Cpu.It    = uint.MaxValue;
    }

    [Test]
    public void InRR_InUserMode_FaultsAndDoesNotReadInput() {
        _serialInput.Enqueue(0xCAFE);
        _vm.Cpu.R2 = 18;
        _vm.Cpu.R1 = 0xAAAA;
        _vm.LoadData([0x47, 0x01, 0x02], 0x100);
        SetupUserMode();
        _vm.Cpu.Ip = 0;

        _vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.True);
            Assert.That(_vm.Cpu.R1, Is.EqualTo(0xAAAAu),
                "Input must NOT have been written to R1");
            Assert.That(_serialInput.Count, Is.EqualTo(1),
                "Input value should still be queued — Input() not invoked");
        });
    }

    [Test]
    public void InRI_InUserMode_Faults() {
        _serialInput.Enqueue(0xCAFE);
        _vm.Cpu.R1 = 0xAAAA;
        _vm.LoadData([0x48, 0x01, 18, 0x00, 0x00, 0x00], 0x100);
        SetupUserMode();
        _vm.Cpu.Ip = 0;

        _vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.True);
            Assert.That(_vm.Cpu.R1, Is.EqualTo(0xAAAAu));
            Assert.That(_serialInput.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void OutRR_InUserMode_FaultsAndDoesNotEmit() {
        _vm.Cpu.R1 = 18;
        _vm.Cpu.R2 = 123;
        _vm.LoadData([0x49, 0x01, 0x02], 0x100);
        SetupUserMode();
        _vm.Cpu.Ip = 0;

        _vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.True);
            Assert.That(_serialOutput, Is.Empty);
        });
    }

    [Test]
    public void OutII_InUserMode_FaultsAndDoesNotEmit() {
        _vm.LoadData([0x4c, 18, 0x00, 0x00, 0x00, 123, 0x00, 0x00, 0x00], 0x100);
        SetupUserMode();
        _vm.Cpu.Ip = 0;

        _vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.True);
            Assert.That(_serialOutput, Is.Empty);
        });
    }

    [Test]
    public void OutRI_InUserMode_FaultsAndDoesNotEmit() {
        _vm.Cpu.R1 = 18;
        _vm.LoadData([0x4a, 0x01, 123, 0x00, 0x00, 0x00], 0x100);
        SetupUserMode();
        _vm.Cpu.Ip = 0;

        _vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.True);
            Assert.That(_serialOutput, Is.Empty);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(0u));
        });
    }

    [Test]
    public void OutIR_InUserMode_FaultsAndDoesNotEmit() {
        _vm.Cpu.R1 = 123;
        _vm.LoadData([0x4b, 18, 0x00, 0x00, 0x00, 0x01], 0x100);
        SetupUserMode();
        _vm.Cpu.Ip = 0;

        _vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.True);
            Assert.That(_serialOutput, Is.Empty);
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(0u));
        });
    }

    [Test]
    public void OutRI_InDriverMode_IsAllowed() {
        _vm.Cpu.R1 = 18;
        _vm.LoadData([0x4a, 0x01, 123, 0x00, 0x00, 0x00], 0x100);
        SetupUserMode();
        _vm.Cpu.Mode = 0b11;
        _vm.Cpu.Ip = 0;

        _vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(_vm.Paused, Is.False);
            Assert.That(_serialOutput, Is.EqualTo(new[] { 123u }));
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
        });
    }
}
