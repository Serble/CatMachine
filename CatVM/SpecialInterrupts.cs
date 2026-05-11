namespace CatVM;

public enum SpecialInterrupts : byte {
    PageFault = 0x00,
    InvalidInstruction = 0x01,
    DivideByZero = 0x02,
    ProtectionFault = 0x03,
    
    // Everything < 0x10 is reserved for CPU exceptions
    
    Syscall = 0x10,
    
    // TODO: Remove these
    HandleInput = 0x70,
    HardwareTimerCallback = 0x71,
    DiskOperationFinish = 0x72,
    NicNotification = 0x73,
    
    FuncWriteStdout = 0x80,
}
