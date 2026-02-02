using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace CatVM.Display.RaylibRenderer;

public class RaylibRendering : IRenderer {
    public const string BgrxShader = 
"""
#version 330

in vec2 fragTexCoord;
out vec4 finalColor;
uniform sampler2D shaderData;

void main() {
    finalColor = vec4(texture(shaderData, fragTexCoord).bgr, 1.0);
}
""";

    public bool DrawFps { get; set; } = true;
    
    private readonly Queue<uint> _serialQueue = [];
    private IDisplayModeRenderer? _renderer;
    
    public void Initialize(CatVM vm) {
        
    }

    private void SetRenderer(CatVM vm) {
        _renderer?.Unload(vm);
        
        Raylib.SetWindowSize(vm.DisplayWidth, vm.DisplayHeight);

        Console.WriteLine("Setting renderer to " + vm.DisplayMode);
        _renderer = ((int)vm.DisplayMode & 0xf0) switch {
            0x00 => new DisplayModeBuffer(vm),
            0x10 => new DisplayModeTiled(vm),
            _ => throw new NotImplementedException($"Display mode {vm.DisplayMode} not implemented!")
        };
    }

    public Task Start(CatVM vm) {
        vm.SerialDevices.Add(0, (
            _ => _serialQueue.TryDequeue(out uint result) ? result : uint.MaxValue,
            (_, _) => {}
        ));
        
        ManualResetEventSlim updateDisplay = new(true);
        
        vm.UpdateDisplayEvent += () => {
            updateDisplay.Reset(); // mark as outdated
            updateDisplay.Wait();  // wait for it to be updated
        };
        
        return Task.Run((Action) (() => {
            unsafe {
                delegate* unmanaged[Cdecl]<int, sbyte*, sbyte*, void> ptr = &NopLogging;
                Raylib.SetTraceLogCallback(ptr);
            }
            
            Raylib.InitWindow(vm.DisplayWidth, vm.DisplayHeight, "CatVM Display");
            Raylib.SetTargetFPS(1024);

            if (!vm.MemoryHandle.HasValue) {
                throw new Exception("Memory not initialized.");
            }
            
            SetRenderer(vm);
            
            bool changeDisplayMode = false;
            vm.DisplayModeUpdated += () => {
                changeDisplayMode = true;
            };
            
            HashSet<KeyboardKey> pressedKeys = [];

            while (!Raylib.WindowShouldClose()) {
                pressedKeys.RemoveWhere(key => {
                    if (!Raylib.IsKeyUp(key)) {
                        return false;
                    }
                        
                    SendInput(vm, 0, 1, (uint)key);
                    return true;
                });
                    
                while (true) {
                    int key = Raylib.GetKeyPressed();
                    if (key == 0) {
                        break;
                    }

                    pressedKeys.Add((KeyboardKey) key);
                    SendInput(vm, 0, 0, (uint)key);
                }

                if (changeDisplayMode) {
                    SetRenderer(vm);
                    changeDisplayMode = false;
                }
                
                // if not up to date, update texture
                if (!updateDisplay.IsSet) {
                    _renderer!.ReadScreenData(vm);
                    updateDisplay.Set();
                }
                
                _renderer!.Update(vm);
                
                Raylib.BeginDrawing();
                _renderer!.Draw(vm);

                if (DrawFps) {
                    Raylib.DrawFPS(8, 8);
                }
                
                Raylib.EndDrawing();
            }
            
            _renderer?.Unload(vm);
            
            Raylib.CloseWindow();
            Environment.Exit(0);
        }));
    }

    private void SendInput(CatVM vm, uint device, uint inputType, uint value) {
        _serialQueue.Enqueue(device);
        _serialQueue.Enqueue(inputType);
        _serialQueue.Enqueue(value);
        vm.Interrupt(SpecialInterupts.HandleInput);
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

        using Stream stream = assembly.GetManifestResourceStream(name)!;
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
}
