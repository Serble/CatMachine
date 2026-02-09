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
}
