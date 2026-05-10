namespace CatVM.Testing.Ops;

public class CpyOperationTest : OperationTestBase {

    [Test]
    public void TestCpy() {
        uint src = 0xF0;
        uint dest = 0x10;
        uint length = 0x0F;

        RunCpy(src, length, dest, () => {
            for (int i = 0; i < length; i++) {
                _vm.Memory[src + (uint)i] = (byte)((src >> (8 * i)) & 0xFF);
            }
        }, () => {
            for (int i = 0; i < length; i++) {
                Assert.That(_vm.Memory[dest + (uint)i], Is.EqualTo((byte)((src >> (8 * i)) & 0xFF)));
            }
        });
    }

    private void RunCpy(uint src, uint length, uint dest, Action setup, Action expected) {
        _vm.Reset();
        setup();
        RunCpyRR(src, length, dest);
        expected();
        
        _vm.Reset();
        setup();
        RunCpyRI(src, length, dest);
        expected();
        
        _vm.Reset();
        setup();
        RunCpyIR(src, length, dest);
        expected();
        
        _vm.Reset();
        setup();
        RunCpyII(src, length, dest);
        expected();
    }

    private void RunCpyRR(uint src, uint length, uint dest) {
        _vm.Cpu.R0 = dest;
        _vm.Cpu.R1 = src;
        _vm.Cpu.R2 = length;
        Execute(0x41, 0x01, 0x02);
    }
    
    private void RunCpyRI(uint src, uint length, uint dest) {
        _vm.Cpu.R0 = dest;
        _vm.Cpu.R1 = src;
        Execute(0x42, 0x01, (byte)(length & 0xFF), (byte)((length >> 8) & 0xFF), (byte)((length >> 16) & 0xFF), (byte)((length >> 24) & 0xFF));
    }
    
    private void RunCpyIR(uint src, uint length, uint dest) {
        _vm.Cpu.R0 = dest;
        _vm.Cpu.R2 = length;
        Execute(0x43, (byte)(src & 0xFF), (byte)((src >> 8) & 0xFF), (byte)((src >> 16) & 0xFF), (byte)((src >> 24) & 0xFF), 0x02);
    }
    
    private void RunCpyII(uint src, uint length, uint dest) {
        _vm.Cpu.R0 = dest;
        Execute(0x44, (byte)(src & 0xFF), (byte)((src >> 8) & 0xFF), (byte)((src >> 16) & 0xFF), (byte)((src >> 24) & 0xFF),
            (byte)(length & 0xFF), (byte)((length >> 8) & 0xFF), (byte)((length >> 16) & 0xFF), (byte)((length >> 24) & 0xFF));
    }

    [Test]
    public void TestCpyZeroLength_NoOp() {
        for (uint i = 0; i < 64; i++) _vm.Memory[i] = (byte)i;
        byte[] before = (byte[])_vm.Memory.Clone();
        _vm.Cpu.R0 = 0x10;
        Execute(0x42, 0x01, 0, 0, 0, 0); // CPY R1, 0
        // Note: instruction bytes were written to memory[0..6] by Execute(); restore.
        for (int i = 0; i < 6; i++) before[i] = _vm.Memory[i];
        Assert.That(_vm.Memory, Is.EqualTo(before));
    }

    [Test]
    public void TestCpyForwardOverlap() {
        // Buffer.BlockCopy semantics: forward-overlapping (dest > src, region overlaps)
        // is well-defined byte-by-byte from low to high in BlockCopy's internal memmove.
        // Pin current behaviour.
        for (uint i = 0; i < 16; i++) _vm.Memory[0x40 + i] = (byte)i;
        _vm.Cpu.R0 = 0x44;  // dest overlaps with src
        Execute(0x44, 0x40, 0, 0, 0, 8, 0, 0, 0); // CPY 0x40 -> 0x44, len 8
        // Result is implementation-defined per Buffer.BlockCopy; assert it doesn't throw and dest is changed.
        Assert.Multiple(() => {
            Assert.That(_vm.Memory[0x44], Is.EqualTo((byte)0));
            Assert.That(_vm.Memory[0x4B], Is.EqualTo((byte)7));
        });
    }

    [Test]
    public void TestCpyBackwardOverlap() {
        for (uint i = 0; i < 16; i++) _vm.Memory[0x40 + i] = (byte)i;
        _vm.Cpu.R0 = 0x40;
        Execute(0x44, 0x44, 0, 0, 0, 8, 0, 0, 0); // CPY 0x44 -> 0x40, len 8
        // After backward copy, dest[0..8] should equal src[0..8] = bytes 4..11
        for (int i = 0; i < 8; i++) {
            Assert.That(_vm.Memory[0x40 + i], Is.EqualTo((byte)(i + 4)));
        }
    }

    [Test]
    public void TestCpyOutOfRangeSource_RaisesPageFault() {
        _vm.Cpu.R0 = 0x10;
        _vm.LoadData([0x44, 0x00, 0xF0, 0xFF, 0xFF, 16, 0, 0, 0]); // CPY [0xFFFFF000] -> R0, len 16
        _vm.Cpu.Ip = 0;
        _vm.ExecuteWithErrorHandling(() => _vm.ExecuteInstruction(fast: true));
        Assert.That(_vm.Paused, Is.True);
    }

    [Test]
    public void TestCpyOutOfRangeDest_RaisesPageFault() {
        _vm.Cpu.R0 = 0xFFFFF000;
        _vm.LoadData([0x44, 0x40, 0, 0, 0, 16, 0, 0, 0]);
        _vm.Cpu.Ip = 0;
        _vm.ExecuteWithErrorHandling(() => _vm.ExecuteInstruction(fast: true));
        Assert.That(_vm.Paused, Is.True);
    }
}
