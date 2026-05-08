namespace CatVM;

public enum SpecialInterupts : byte {
    PageFault = 0x00,
    InvalidInstruction = 0x01,
    DivideByZero = 0x02,
    
    // Everything < 0x10 is reserved for CPU exceptions
    
    HandleInput = 0x70,
    HardwareTimerCallback = 0x71,
    DiskOperationFinish = 0x72,
    NicNotification = 0x73,
    FuncWriteStdout = 0x80,
}
