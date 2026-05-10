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
    }
    
    [Test]
    public void TestInRI() {
        _serialInput.Enqueue(123);
        Execute(0x48, 0x01, 18, 0x00, 0x00, 0x00);  // IN R1, 18
        Assert.That(_vm.Cpu.R1, Is.EqualTo(123));
    }

    [Test]
    public void TestOutRR() {
        _vm.Cpu.R1 = 18;
        _vm.Cpu.R2 = 123;
        _serialOutput.Clear();
        Execute(0x49, 0x01, 0x02);  // OUT R1, R2
        Assert.That(_serialOutput, Is.EqualTo([123]));
    }

    [Test]
    public void TestOutRI() {
        _vm.Cpu.R1 = 18;
        _serialOutput.Clear();
        Execute(0x4a, 0x01, 123, 0x00, 0x00, 0x00); // OUT R1, 18
        Assert.That(_serialOutput, Is.EqualTo([123]));
    }
    
    [Test]
    public void TestOutIR() {
        _vm.Cpu.R1 = 123;
        _serialOutput.Clear();
        Execute(0x4b, 18, 0x00, 0x00, 0x00, 0x01); // OUT 18, R1
        Assert.That(_serialOutput, Is.EqualTo([123]));
    }
    
    [Test]
    public void TestOutII() {
        _serialOutput.Clear();
        Execute(0x4c, 18, 0x00, 0x00, 0x00, 123, 0x00, 0x00, 0x00); // OUT 18, 18
        Assert.That(_serialOutput, Is.EqualTo([123]));
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
        });
    }
}
