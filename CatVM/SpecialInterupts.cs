namespace CatVM;

public enum SpecialInterupts : byte {
    PageFault = 0x00,
    InvalidInstruction = 0x01,
    DivideByZero = 0x02,
    
    // Everything < 0x10 is reserved for CPU exceptions
    
    FuncWriteStdout = 0x80
}
