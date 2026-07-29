namespace CatVM.Testing.Ops;

public class MovOperationTest : OperationTestBase {
    
    [Test]
    public void TestMovRR() {
        _vm.Cpu.R1 = 0x12345678;
        Execute(0x00, 0x02, 0x01);  // MOV R2, R1
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x12345678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestMovRI() {
        Execute(0x01, 0x02, 0x78, 0x56, 0x34, 0x12);  // MOV R2, 0x12345678
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x12345678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
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
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestMovRIP() {
        _vm.Memory[0x30] = 0x78;
        _vm.Memory[0x31] = 0x56;
        _vm.Memory[0x32] = 0x34;
        _vm.Memory[0x33] = 0x12;  // [0x30] = 0x12345678
        Execute(0x03, 0x02, 0x30, 0x00, 0x00, 0x00);  // MOV R2, [0x30]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x12345678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }
    
    [Test]
    public void TestMovRPR() {
        _vm.Cpu.R1 = 0x30;  // R1 points to the destination
        _vm.Cpu.R2 = 0x12345678;  // value to store
        Execute(0x04, 0x01, 0x02);  // MOV [R1], R2
        Assert.That(BitConverter.ToUInt32(_vm.Memory, 0x30), Is.EqualTo(0x12345678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestMovRPI() {
        _vm.Cpu.R1 = 0x30;  // R1 points to the destination
        Execute(0x05, 0x01, 0x78, 0x56, 0x34, 0x12);  // MOV [R1], 0x12345678
        Assert.That(BitConverter.ToUInt32(_vm.Memory, 0x30), Is.EqualTo(0x12345678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }
    
    [Test]
    public void TestMovIPR() {
        _vm.Cpu.R1 = 0x12345678;  // value to store
        Execute(0x06, 0x30, 0x00, 0x00, 0x00, 0x01);  // MOV [0x30], R1
        Assert.That(BitConverter.ToUInt32(_vm.Memory, 0x30), Is.EqualTo(0x12345678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }
    
    [Test]
    public void TestMovIPI() {
        Execute(0x07, 0x30, 0x00, 0x00, 0x00, 0x78, 0x56, 0x34, 0x12);  // MOV [0x30], 0x12345678
        Assert.That(BitConverter.ToUInt32(_vm.Memory, 0x30), Is.EqualTo(0x12345678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(9u));
    }
    
    [Test]
    public void TestMov16RRP() {
        _vm.Memory[0x30] = 0x78;
        _vm.Memory[0x31] = 0x56;  // [0x30] = 0x5678
        _vm.Cpu.R1 = 0x30;  // R1 points to the value
        Execute(0x08, 0x02, 0x01);  // MOV16 R2, [R1]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x5678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }

    [Test]
    public void TestMov16RIP() {
        _vm.Memory[0x30] = 0x78;
        _vm.Memory[0x31] = 0x56; // [0x30] = 0x5678
        Execute(0x09, 0x02, 0x30, 0x00, 0x00, 0x00); // MOV16 R2, [0x30]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x5678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }
    
    [Test]
    public void TestMov16RPR() {
        _vm.Cpu.R1 = 0x30;  // R1 points to the destination
        _vm.Cpu.R2 = 0x12345678;  // value to store (only lower 16 bits should be stored)
        Execute(0x0A, 0x01, 0x02);  // MOV16 [R1], R2
        Assert.That(BitConverter.ToUInt16(_vm.Memory, 0x30), Is.EqualTo(0x5678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }

    [Test]
    public void TestMov16RPI() {
        _vm.Cpu.R1 = 0x30; // R1 points to the destination
        Execute(0x0B, 0x01, 0x78, 0x56); // MOV16 [R1], 0x5678
        Assert.That(BitConverter.ToUInt16(_vm.Memory, 0x30), Is.EqualTo(0x5678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(4u));
    }
    
    [Test]
    public void TestMov16IPR() {
        _vm.Cpu.R1 = 0x12345678; // value to store (only lower 16 bits should be stored)
        Execute(0x0C, 0x30, 0x00, 0x00, 0x00, 0x01); // MOV16 [0x30], R1
        Assert.That(BitConverter.ToUInt16(_vm.Memory, 0x30), Is.EqualTo(0x5678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestMov16IPI() {
        Execute(0x0D, 0x30, 0x00, 0x00, 0x00, 0x78, 0x56); // MOV16 [0x30], 0x5678
        Assert.That(BitConverter.ToUInt16(_vm.Memory, 0x30), Is.EqualTo(0x5678));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(7u));
    }
    
    [Test]
    public void TestMov8RRP() {
        _vm.Memory[0x30] = 0x78; // [0x30] = 0x78
        _vm.Cpu.R1 = 0x30; // R1 points to the value
        Execute(0x0E, 0x02, 0x01); // MOV8 R2, [R1]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x78));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestMov8RIP() {
        _vm.Memory[0x30] = 0x78; // [0x30] = 0x78
        Execute(0x0F, 0x02, 0x30, 0x00, 0x00, 0x00); // MOV8 R2, [0x30]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0x78));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }
    
    [Test]
    public void TestMov8RPR() {
        _vm.Cpu.R1 = 0x30; // R1 points to the destination
        _vm.Cpu.R2 = 0x78; // value to store
        Execute(0x10, 0x01, 0x02); // MOV8 [R1], R2
        Assert.That(_vm.Memory[0x30], Is.EqualTo(0x78));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestMov8RPI() {
        _vm.Cpu.R1 = 0x30; // R1 points to the destination
        Execute(0x11, 0x01, 0x78); // MOV8 [R1], 0x78
        Assert.That(_vm.Memory[0x30], Is.EqualTo(0x78));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(3u));
    }
    
    [Test]
    public void TestMov8IPR() {
        _vm.Cpu.R1 = 0x78; // value to store
        Execute(0x12, 0x30, 0x00, 0x00, 0x00, 0x01); // MOV8 [0x30], R1
        Assert.That(_vm.Memory[0x30], Is.EqualTo(0x78));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }
    
    [Test]
    public void TestMov8IPI() {
        Execute(0x13, 0x30, 0x00, 0x00, 0x00, 0x78); // MOV8 [0x30], 0x78
        Assert.That(_vm.Memory[0x30], Is.EqualTo(0x78));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    // ---- Edge cases ----

    [Test]
    public void TestBMovZeroExtends() {
        // BMov from memory loads byte and sign-extends as zero into upper bits
        _vm.Memory[0x30] = 0xFF;
        _vm.Cpu.R2 = 0xAAAAAAAA;
        Execute(0x0F, 0x02, 0x30, 0x00, 0x00, 0x00); // BMov R2, [0x30]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0xFFu));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestSMovZeroExtends() {
        _vm.Memory[0x30] = 0xFF;
        _vm.Memory[0x31] = 0xFF;
        _vm.Cpu.R2 = 0xAAAAAAAA;
        Execute(0x09, 0x02, 0x30, 0x00, 0x00, 0x00); // SMov R2, [0x30]
        Assert.That(_vm.Cpu.R2, Is.EqualTo(0xFFFFu));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void TestMovOutOfRangeRead_RaisesPageFault() {
        _vm.LoadData([0x03, 0x02, 0x00, 0xF0, 0xFF, 0xFF]); // MOV R2, [0xFFFFF000]
        _vm.Cpu.Ip = 0;
        _vm.ExecuteWithErrorHandling(() => _vm.ExecuteInstruction(fast: true));
        Assert.That(_vm.Paused, Is.True);
    }

    [Test]
    public void TestMovOutOfRangeWrite_RaisesPageFault() {
        _vm.LoadData([0x07, 0x00, 0xF0, 0xFF, 0xFF, 0x78, 0x56, 0x34, 0x12]); // MOV [0xFFFFF000], 0x12345678
        _vm.Cpu.Ip = 0;
        _vm.ExecuteWithErrorHandling(() => _vm.ExecuteInstruction(fast: true));
        Assert.That(_vm.Paused, Is.True);
    }

    [Test]
    public void TestMovUnalignedAddress_Works() {
        // 32-bit access at an odd address should work (Unsafe.WriteUnaligned).
        Execute(0x07, 0x33, 0x00, 0x00, 0x00, 0x78, 0x56, 0x34, 0x12);
        Assert.That(BitConverter.ToUInt32(_vm.Memory, 0x33), Is.EqualTo(0x12345678u));
        Assert.That(_vm.Cpu.Ip, Is.EqualTo(9u));
    }

    [Test]
    public void TestMovInVirtualMode_InstructionFetchUsesMBase() {
        // Verifies *IP* translation: a MOV R2, 0x12345678 placed at physical mbase
        // is executed via virtual IP=0 in user mode.
        _vm.Reset();
        const uint mbase = 0x100;
        const uint mlen  = 0x80;
        _vm.LoadData([0x01, 0x02, 0x78, 0x56, 0x34, 0x12], mbase); // MOV R2, 0x12345678
        _vm.Cpu.MBase = mbase;
        _vm.Cpu.MLen = mlen;
        _vm.Cpu.Sp = mlen;
        _vm.Cpu.Mode = 0b01;
        _vm.Cpu.Ip = 0;
        _vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(_vm.Cpu.R2, Is.EqualTo(0x12345678u));
            Assert.That(_vm.Cpu.Ip, Is.EqualTo(6u), "Virtual IP advances normally");
        });
    }

    [Test]
    public void TestVirtualMode_IpBeyondMLen_RaisesPageFault() {
        _vm.Reset();
        const uint mbase = 0x40;
        const uint mlen  = 0x10;
        _vm.Cpu.MBase = mbase;
        _vm.Cpu.MLen = mlen;
        _vm.Cpu.Sp = mlen;
        _vm.Cpu.Mode = 0b01;
        _vm.Cpu.Ip = mlen;  // exactly out-of-range for any read
        _vm.ExecuteWithErrorHandling(() => _vm.ExecuteInstruction(fast: true));
        Assert.That(_vm.Paused, Is.True);
    }
}
