namespace CatVM.Testing.Ops;

public class MovOperationTest : OperationTestBase {
    
    [Test]
    public void TestMovRR() {
        _vm.Cpu.R1 = 0x12345678;
        Execute(0x00, 0x02, 0x01);  // MOV R2, R1
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x12345678));
    }
    
    [Test]
    public void TestMovRI() {
        Execute(0x01, 0x02, 0x78, 0x56, 0x34, 0x12);  // MOV R2, 0x12345678
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x12345678));
    }

    [Test]
    public void TestMovRRP() {
        _vm.Memory[0x30] = 0x78;
        _vm.Memory[0x31] = 0x56;
        _vm.Memory[0x32] = 0x34;
        _vm.Memory[0x33] = 0x12;  // [0x30] = 0x12345678
        _vm.Cpu.R1 = 0x30;  // R1 points to the value
        Execute(0x02, 0x02, 0x01);  // MOV R2, [R1]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x12345678));
    }
    
    [Test]
    public void TestMovRIP() {
        _vm.Memory[0x30] = 0x78;
        _vm.Memory[0x31] = 0x56;
        _vm.Memory[0x32] = 0x34;
        _vm.Memory[0x33] = 0x12;  // [0x30] = 0x12345678
        Execute(0x03, 0x02, 0x30, 0x00, 0x00, 0x00);  // MOV R2, [0x30]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x12345678));
    }
    
    [Test]
    public void TestMovRPR() {
        _vm.Cpu.R1 = 0x30;  // R1 points to the destination
        _vm.Cpu.R2 = 0x12345678;  // value to store
        Execute(0x04, 0x01, 0x02);  // MOV [R1], R2
        Assert.That(BitConverter.ToUInt32(_vm.Memory, 0x30), Is.EqualTo(0x12345678));
    }
    
    [Test]
    public void TestMovRPI() {
        _vm.Cpu.R1 = 0x30;  // R1 points to the destination
        Execute(0x05, 0x01, 0x78, 0x56, 0x34, 0x12);  // MOV [R1], 0x12345678
        Assert.That(BitConverter.ToUInt32(_vm.Memory, 0x30), Is.EqualTo(0x12345678));
    }
    
    [Test]
    public void TestMovIPR() {
        _vm.Cpu.R1 = 0x12345678;  // value to store
        Execute(0x06, 0x30, 0x00, 0x00, 0x00, 0x01);  // MOV [0x30], R1
        Assert.That(BitConverter.ToUInt32(_vm.Memory, 0x30), Is.EqualTo(0x12345678));
    }
    
    [Test]
    public void TestMovIPI() {
        Execute(0x07, 0x30, 0x00, 0x00, 0x00, 0x78, 0x56, 0x34, 0x12);  // MOV [0x30], 0x12345678
        Assert.That(BitConverter.ToUInt32(_vm.Memory, 0x30), Is.EqualTo(0x12345678));
    }
    
    [Test]
    public void TestMov16RRP() {
        _vm.Memory[0x30] = 0x78;
        _vm.Memory[0x31] = 0x56;  // [0x30] = 0x5678
        _vm.Cpu.R1 = 0x30;  // R1 points to the value
        Execute(0x08, 0x02, 0x01);  // MOV16 R2, [R1]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x5678));
    }

    [Test]
    public void TestMov16RIP() {
        _vm.Memory[0x30] = 0x78;
        _vm.Memory[0x31] = 0x56; // [0x30] = 0x5678
        Execute(0x09, 0x02, 0x30, 0x00, 0x00, 0x00); // MOV16 R2, [0x30]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x5678));
    }
    
    [Test]
    public void TestMov16RPR() {
        _vm.Cpu.R1 = 0x30;  // R1 points to the destination
        _vm.Cpu.R2 = 0x12345678;  // value to store (only lower 16 bits should be stored)
        Execute(0x0A, 0x01, 0x02);  // MOV16 [R1], R2
        Assert.That(BitConverter.ToUInt16(_vm.Memory, 0x30), Is.EqualTo(0x5678));
    }

    [Test]
    public void TestMov16RPI() {
        _vm.Cpu.R1 = 0x30; // R1 points to the destination
        Execute(0x0B, 0x01, 0x78, 0x56); // MOV16 [R1], 0x5678
        Assert.That(BitConverter.ToUInt16(_vm.Memory, 0x30), Is.EqualTo(0x5678));
    }
    
    [Test]
    public void TestMov16IPR() {
        _vm.Cpu.R1 = 0x12345678; // value to store (only lower 16 bits should be stored)
        Execute(0x0C, 0x30, 0x00, 0x00, 0x00, 0x01); // MOV16 [0x30], R1
        Assert.That(BitConverter.ToUInt16(_vm.Memory, 0x30), Is.EqualTo(0x5678));
    }

    [Test]
    public void TestMov16IPI() {
        Execute(0x0D, 0x30, 0x00, 0x00, 0x00, 0x78, 0x56); // MOV16 [0x30], 0x5678
        Assert.That(BitConverter.ToUInt16(_vm.Memory, 0x30), Is.EqualTo(0x5678));
    }
    
    [Test]
    public void TestMov8RRP() {
        _vm.Memory[0x30] = 0x78; // [0x30] = 0x78
        _vm.Cpu.R1 = 0x30; // R1 points to the value
        Execute(0x0E, 0x02, 0x01); // MOV8 R2, [R1]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x78));
    }
    
    [Test]
    public void TestMov8RIP() {
        _vm.Memory[0x30] = 0x78; // [0x30] = 0x78
        Execute(0x0F, 0x02, 0x30, 0x00, 0x00, 0x00); // MOV8 R2, [0x30]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x78));
    }
    
    [Test]
    public void TestMov8RPR() {
        _vm.Cpu.R1 = 0x30; // R1 points to the destination
        _vm.Cpu.R2 = 0x78; // value to store
        Execute(0x10, 0x01, 0x02); // MOV8 [R1], R2
        Assert.That(_vm.Memory[0x30], Is.EqualTo(0x78));
    }
    
    [Test]
    public void TestMov8RPI() {
        _vm.Cpu.R1 = 0x30; // R1 points to the destination
        Execute(0x11, 0x01, 0x78); // MOV8 [R1], 0x78
        Assert.That(_vm.Memory[0x30], Is.EqualTo(0x78));
    }
    
    [Test]
    public void TestMov8IPR() {
        _vm.Cpu.R1 = 0x78; // value to store
        Execute(0x12, 0x30, 0x00, 0x00, 0x00, 0x01); // MOV8 [0x30], R1
        Assert.That(_vm.Memory[0x30], Is.EqualTo(0x78));
    }
    
    [Test]
    public void TestMov8IPI() {
        Execute(0x13, 0x30, 0x00, 0x00, 0x00, 0x78); // MOV8 [0x30], 0x78
        Assert.That(_vm.Memory[0x30], Is.EqualTo(0x78));
    }
}
