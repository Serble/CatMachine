using CatVM.Serial;

namespace CatVM.Testing.Ops;

public class SerialOperationTest : OperationTestBase {
    private List<uint> _serialOutput = [];
    private Queue<uint> _serialInput = [];
    
    [SetUp]
    public void RegisterSerial() {
        _vm.RegisterSerialDevice(18, new SerialDevice(
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
}
