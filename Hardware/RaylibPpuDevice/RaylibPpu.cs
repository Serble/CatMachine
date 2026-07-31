using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CatData;
using CatVM;
using CatVM.Serial;
using Raylib_cs;

namespace RaylibPpuDevice;

public class RaylibPpu {
    public bool DrawFps { get; set; }

    /// <summary>
    /// Whether the display window should cover the whole monitor with no decorations and hide the
    /// host cursor. Used by CatVM.Metal so that the guest display looks like the machine's own
    /// display instead of a window.
    /// <p/>
    /// This is read when the window is created, which does not happen until the guest leaves
    /// <see cref="DisplayMode.DummyDisplay"/>, so it is safe to set right after construction.
    /// </summary>
    public bool Fullscreen { get; set; }

    public GraphicsDevice Graphics { get; private init; }
    public KeyboardInputDevice Keyboard { get; private init; }
    public MouseInputDevice Mouse { get; private init; }
    public List<InputDevice> InputDevices { get; private init; }
    
    private IDisplayModeRenderer? _renderer;
    private readonly ManualResetEventSlim _updateDisplay = new(true);
    private readonly ManualResetEventSlim _changeDisplayMode = new(true);
    
    public int DisplayWidth { get; private set; }
    public int DisplayHeight { get; private set; }
    
    public DisplayMode DisplayMode { get;
        set {
            field = value;

            if (value == DisplayMode.DummyDisplay) {
                DisplayWidth = 0;
                DisplayHeight = 0;
            }
            else {
                switch ((int)value & 0xf) {
                    case 0:
                        DisplayWidth = 512;
                        DisplayHeight = 512;
                        break;
                
                    case 1:
                        DisplayWidth = 512;
                        DisplayHeight = 384;
                        break;
                }
            }

            _changeDisplayMode.Reset();
        }
    } = DisplayMode.DummyDisplay;
    
    /// <summary>
    /// The size of the display buffer in bytes for the current display mode.
    /// Refer to <see cref="DisplayBufferAddress"/> for where the display buffer is located in memory.
    /// </summary>
    public int DisplayBufferSize {
        get {
            if (DisplayMode == DisplayMode.DummyDisplay) {
                return 0;
            }
            
            return ((int)DisplayMode & 0xf) switch {
                0 => DisplayWidth * DisplayHeight * 4,
                1 => 34_868,
                _ => 0
            };
        }
    }
    
    /// <summary>
    /// The address of the display buffer in the VM's memory.
    /// <remarks>This should only really be set by user program.</remarks>
    /// </summary>
    public uint DisplayBufferAddress { get; set; }

    [CommandLineConstructable("RaylibPpu", false, ["graphicsPort", "keyboardPort", "mousePort"])]
    public RaylibPpu(CatVm vm, uint? graphicsPort = null, uint? keyboardPort = null, uint? mousePort = null) {
        Graphics = new GraphicsDevice(this);
        Keyboard = new KeyboardInputDevice();
        Mouse = new MouseInputDevice();
        InputDevices = [Keyboard, Mouse];
        vm.RegisterSerialDevice(graphicsPort, Graphics);
        vm.RegisterSerialDevice(keyboardPort, Keyboard);
        vm.RegisterSerialDevice(mousePort, Mouse);
        Task.Run(() => Start(vm));
    }

    public class GraphicsDevice(RaylibPpu ppu) : CommandBasedSerialDevice<GraphicsDevice.Mode> {
        public override uint Type => 0xFF64BEF9;

        protected override int GetArgCount(Mode mode) {
            return mode switch {
                Mode.UpdateDisplay => 0,
                Mode.ChangeDisplayMode => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };
        }
        
        protected override void RunMode(CatVm vm, Mode mode, List<uint> args) {
            switch (mode) {
                case Mode.UpdateDisplay:
                    ppu._updateDisplay.Reset(); // mark as outdated
                    ppu._updateDisplay.Wait();  // wait for it to be updated
                    break;
                
                case Mode.ChangeDisplayMode:
                    uint displayMode = args[0];
                    if (!Enum.IsDefined(typeof(DisplayMode), (int)displayMode)) {
                        InputQueue.Enqueue(1);
                        break;
                    }

                    DisplayMode oldMode = ppu.DisplayMode;
                    uint oldOffset = ppu.DisplayBufferAddress;
                    ppu.DisplayMode = (DisplayMode)displayMode;
                    ppu.DisplayBufferAddress = args[1];

                    if (ppu.DisplayBufferAddress + ppu.DisplayBufferSize > vm.Memory.Length) {
                        InputQueue.Enqueue(2);
                        ppu.DisplayMode = oldMode;
                        ppu.DisplayBufferAddress = oldOffset;
                        break;
                    }
        
                    InputQueue.Enqueue(0);
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
        
        public enum Mode {
            UpdateDisplay     = 1,
            ChangeDisplayMode = 2,
        }
    }
    
    public abstract class InputDevice : ISerialDevice {
        public abstract uint Type { get; }

        private byte _interruptCode = 0x70;
        private Queue<uint> _inputsQueue = new();

        public uint Input(CatVm vm) {
            return _inputsQueue.Count == 0 ? uint.MaxValue : _inputsQueue.Dequeue();
        }

        public void Output(CatVm vm, uint data) {
            _interruptCode = (byte)data;
        }

        public void SendInput(CatVm vm, uint inputType, uint value) {
            _inputsQueue.Enqueue(inputType);
            _inputsQueue.Enqueue(value);
            vm.HardwareInterrupt(_interruptCode);
        }
    }
    
    public class KeyboardInputDevice : InputDevice {
        public override uint Type => 0x2EB3AD76;
    }
    
    public class MouseInputDevice : InputDevice {
        public override uint Type => 0x25A3E57D;
    }
    
    private void SetRenderer(CatVm vm) {
        _renderer?.Unload(this, vm);

        // Console.WriteLine("Setting renderer to " + vm.DisplayMode);
        _renderer = ((int)DisplayMode & 0xf0) switch {
            0x00 => new DisplayModeBuffer(this),
            0x10 => new DisplayModeTiled(this),
            _ => throw new NotImplementedException($"Display mode {DisplayMode} not implemented!")
        };
    }

    private void Start(CatVm vm) {
        start:
        
        // wait for display mode to not be DummyDisplay
        while (_changeDisplayMode.IsSet) {
            Thread.Sleep(8);
        }
        _changeDisplayMode.Set();
        
        if (DisplayMode == DisplayMode.DummyDisplay) {
            goto start;
        }
        
        unsafe {
            delegate* unmanaged[Cdecl]<int, sbyte*, sbyte*, void> ptr = &NopLogging;
            Raylib.SetTraceLogCallback(ptr);
        }
        
        Raylib.InitWindow(DisplayWidth, DisplayHeight, "CatVM Display");
        Raylib.SetWindowState(ConfigFlags.ResizableWindow);
        Raylib.SetTargetFPS(1024);
        Raylib.SetExitKey(0);

        if (Fullscreen) {
            // Borderless rather than a real mode switch: the guest resolutions are tiny and rarely
            // exist as video modes, so we take the whole monitor and letterbox into it instead.
            Raylib.SetWindowState(ConfigFlags.BorderlessWindowMode);
            Raylib.HideCursor();
        }

        if (!vm.MemoryHandle.HasValue) {
            throw new Exception("Memory not initialized.");
        }
        
        SetRenderer(vm);
        
        HashSet<KeyboardKey> pressedKeys = [];
        int lastMouseX = -1;
        int lastMouseY = -1;
        
        while (!Raylib.WindowShouldClose()) {
            pressedKeys.RemoveWhere(key => {
                if (!Raylib.IsKeyUp(key)) {
                    return false;
                }
                    
                Keyboard.SendInput(vm, 0, (uint)key);
                return true;
            });
            
            while (true) {
                int key = Raylib.GetKeyPressed();
                if (key == 0) {
                    break;
                }
                
                pressedKeys.Add((KeyboardKey) key);
                Keyboard.SendInput(vm, 1, (uint)key);
            }

            foreach (MouseButton button in Enum.GetValues<MouseButton>()) {
                if (Raylib.IsMouseButtonPressed(button)) {
                    Mouse.SendInput(vm, 1, (uint)button);
                }
                else if (Raylib.IsMouseButtonReleased(button)) {
                    Mouse.SendInput(vm, 0, (uint)button);
                }
            }

            Rectangle bounds = GetCenteredBounds();
            Vector2 mousePos = (Raylib.GetMousePosition() - bounds.Position) / bounds.Size * new Vector2(DisplayWidth, DisplayHeight);
            int mousePosX = Math.Clamp((int)mousePos.X, 0, DisplayWidth);
            int mousePosY = Math.Clamp((int)mousePos.Y, 0, DisplayHeight);

            if (mousePosX != lastMouseX || mousePosY != lastMouseY) {
                lastMouseX = mousePosX;
                lastMouseY = mousePosY;
                Mouse.SendInput(vm, 2, (uint)((ushort)mousePosX | ((ushort)mousePosY << 16)));
            }
            
            if (!_changeDisplayMode.IsSet) {
                if (DisplayMode == DisplayMode.DummyDisplay) {
                    _renderer?.Unload(this, vm);
                    Raylib.CloseWindow();
                    goto start;
                }
                
                SetRenderer(vm);
                _changeDisplayMode.Set();
            }
            
            // if not up to date, update texture
            if (!_updateDisplay.IsSet) {
                _renderer!.ReadScreenData(this, vm);
                _updateDisplay.Set();
            }
            
            _renderer!.Update(this, vm);
            
            Raylib.BeginDrawing();
            _renderer!.Draw(this, vm);
            
            if (DrawFps) {
                Raylib.DrawFPS(8, 8);
            }
            
            Raylib.EndDrawing();
        }
        
        _renderer?.Unload(this, vm);
        
        Raylib.CloseWindow();
        Environment.Exit(0);
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void NopLogging(int logLevel, sbyte* msg, sbyte* args) {
        
    }

    public static Color BgrxToColor(uint value) {
        return new Color((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }
    
    public static Color BgraToColor(uint value) {
        return new Color((byte)(value >> 16), (byte)(value >> 8), (byte)value, (byte)(value >> 24));
    }

    public static string ReadResource(string name) {
        Assembly assembly = Assembly.GetExecutingAssembly();

        string resourceName = assembly.GetManifestResourceNames().First(resource => resource.EndsWith(name));
        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    public static Texture2D CreateTexture(int width, int height, PixelFormat format, int bytesPerPixel) {
        unsafe {
            byte[] blankImage = new byte[width * height * bytesPerPixel];
            GCHandle alloc = GCHandle.Alloc(blankImage, GCHandleType.Pinned);
                
            Image image = new() {
                Data = alloc.AddrOfPinnedObject().ToPointer(),
                Width = width,
                Height = height,
                Mipmaps = 1,
                Format = format
            };
                
            Texture2D texture = Raylib.LoadTextureFromImage(image);
            
            alloc.Free();
            
            return texture;
        }
    }

    public Rectangle GetCenteredBounds() {
        int width = Raylib.GetRenderWidth();
        int height = Raylib.GetRenderHeight();
        int innerWidth = width;
        int innerHeight = height;
        
        float aspect = (float)DisplayWidth / DisplayHeight;

        // convert height to relative width and see which is bigger
        if (width > height * aspect) {
            innerWidth = (int)(height * aspect);
        }
        else {
            innerHeight = (int)(width / aspect);
        }

        return new Rectangle(MathF.Round((width - innerWidth) / 2.0f), 
            MathF.Round((height - innerHeight) / 2.0f), 
            innerWidth, innerHeight);
    }
}
