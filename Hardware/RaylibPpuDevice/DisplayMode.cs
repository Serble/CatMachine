namespace RaylibPpuDevice;

public enum DisplayMode {
    Raw512X512   = 0x00,
    Raw512X384   = 0x01,
    
    Tiled512X384 = 0x11,
    
    DummyDisplay = 0xFF
}
