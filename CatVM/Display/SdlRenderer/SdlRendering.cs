using System.Reflection;
using CatVM.Serial;
using SDL3;

namespace CatVM.Display.SdlRenderer;

public class SdlRendering : IRenderer {
    private readonly Queue<uint> _serialQueue = [];
    private IDisplayModeRenderer? _renderer;
    
    public void Initialize(CatVM vm) { }
    
    private void SetRenderer(CatVM vm, nint window, nint renderer) {
        _renderer?.Unload();

        // Console.WriteLine("Setting renderer to " + vm.DisplayMode);
        _renderer = ((int)vm.DisplayMode & 0xf0) switch {
            0x00 => new DisplayModeBuffer(vm, window, renderer),
            0x10 => new DisplayModeTiled(vm, window, renderer),
            _ => throw new NotImplementedException($"Display mode {vm.DisplayMode} not implemented!")
        };
    }
    
    public Task Start(CatVM vm) {
        vm.RegisterSerialDevice(0, ISerialDevice.Create(
            _ => _serialQueue.TryDequeue(out uint result) ? result : uint.MaxValue,
            (_, _) => {})
        );
        
        ManualResetEventSlim updateDisplay = new(true);
        
        bool changeDisplayMode = false;
        vm.DisplayModeUpdated += () => {
            changeDisplayMode = true;
        };
        
        vm.UpdateDisplayEvent += () => {
            updateDisplay.Reset(); // mark as outdated
            updateDisplay.Wait();  // wait for it to be updated
        };

        TaskCompletionSource task = new();

        Thread thread = new(() => {
            if (!SDL.Init(SDL.InitFlags.Video)) {
                SDL.LogError(SDL.LogCategory.System, $"SDL could not initialize: {SDL.GetError()}");
                return;
            }

            start:

            // wait for display mode to not be DummyDisplay
            while (!changeDisplayMode) {
                Thread.Sleep(8);
            }

            if (vm.DisplayMode == DisplayMode.DummyDisplay) {
                changeDisplayMode = false;
                goto start;
            }

            if (!SDL.CreateWindowAndRenderer("CatVM Display", vm.DisplayWidth, vm.DisplayHeight,
                    SDL.WindowFlags.Resizable, out nint window, out nint renderer)) {
                SDL.LogError(SDL.LogCategory.Application, $"Error creating window and rendering: {SDL.GetError()}");
                return;
            }
            
            if (!vm.MemoryHandle.HasValue) {
                throw new Exception("Memory not initialized.");
            }
            
            SetRenderer(vm, window, renderer);

            ulong startCounter = SDL.GetPerformanceCounter();
            ulong frequency = SDL.GetPerformanceFrequency();

            while (true) {
                while (SDL.PollEvent(out SDL.Event e)) {
                    switch ((SDL.EventType)e.Type) {
                        case SDL.EventType.Quit:
                            SDL.DestroyRenderer(renderer);
                            SDL.DestroyWindow(window);
                            SDL.Quit();
                            Environment.Exit(0);
                            break;
                        
                        case SDL.EventType.KeyUp:
                        case SDL.EventType.KeyDown: {
                            if (e.Key.Repeat) {
                                break;
                            }
                            
                            SendInput(vm, 0, (uint)(e.Key.Down ? 0 : 1), MapKeycode(e.Key.Key));
                            break;
                        }
                    }
                }

                ulong currentCounter = SDL.GetPerformanceCounter();
                double elapsed = (currentCounter - startCounter) / (double)frequency;
                
                if (changeDisplayMode) {
                    if (vm.DisplayMode == DisplayMode.DummyDisplay) {
                        _renderer?.Unload();
                        SDL.DestroyRenderer(renderer);
                        SDL.DestroyWindow(window);
                        goto start;
                    }
                    
                    SetRenderer(vm, window, renderer);
                    changeDisplayMode = false;
                }
                
                // if not up to date, update texture
                if (!updateDisplay.IsSet) {
                    _renderer!.ReadScreenData();
                    updateDisplay.Set();
                }
                
                _renderer!.Draw();
            }
        });
        
        if (OperatingSystem.IsWindows()) {
            thread.SetApartmentState(ApartmentState.STA);
        }
        
        thread.Start();
        return task.Task;
    }
    
    private void SendInput(CatVM vm, uint device, uint inputType, uint value) {
        Console.WriteLine($"{device} {inputType} {value}");
        _serialQueue.Enqueue(device);
        _serialQueue.Enqueue(inputType);
        _serialQueue.Enqueue(value);
        vm.HardwareInterrupt(SpecialInterupts.HandleInput);
    }
    
    public static byte[] ReadResourceBytes(string name) {
        Assembly assembly = Assembly.GetExecutingAssembly();

        string resourceName = assembly.GetManifestResourceNames().First(resource => resource.EndsWith(name));
        using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    public static nint LoadShader(string name, nint renderer, SDL.GPUShaderStage stage) {
        byte[] tileVertex = ReadResourceBytes(name);
        unsafe {
            fixed (byte* vs = tileVertex) {
                SDL.GPUShaderCreateInfo shaderCreateInfo = new() {
                    Stage = stage,
                    Entrypoint = "main", // Your GLSL entry point
                    Code = (nint)vs,
                    Format = SDL.GPUShaderFormat.SPIRV // Or SPIRV if pre-compiled
                };

                return SDL.CreateGPUShader(SDL.GetGPURendererDevice(renderer), shaderCreateInfo);
            }
        }
    }

    public static SDL.FRect GetCenteredBounds(CatVM vm, nint window) {
        SDL.GetWindowSize(window, out int width, out int height);
        int innerWidth = width;
        int innerHeight = height;

        float aspect = (float)vm.DisplayWidth / vm.DisplayHeight;

        // convert height to relative width and see which is bigger
        if (width > height * aspect) {
            innerWidth = (int)(height * aspect);
        }
        else {
            innerHeight = (int)(width / aspect);
        }

        return new SDL.FRect {
            X = MathF.Round((width - innerWidth) / 2.0f), 
            Y = MathF.Round((height - innerHeight) / 2.0f),
            W = innerWidth,
            H = innerHeight 
        };
    }
    
    public static uint MapKeycode(SDL.Keycode key) {
        return key switch {
            // Alphanumeric
            SDL.Keycode.A => 65,
            SDL.Keycode.B => 66,
            SDL.Keycode.C => 67,
            SDL.Keycode.D => 68,
            SDL.Keycode.E => 69,
            SDL.Keycode.F => 70,
            SDL.Keycode.G => 71,
            SDL.Keycode.H => 72,
            SDL.Keycode.I => 73,
            SDL.Keycode.J => 74,
            SDL.Keycode.K => 75,
            SDL.Keycode.L => 76,
            SDL.Keycode.M => 77,
            SDL.Keycode.N => 78,
            SDL.Keycode.O => 79,
            SDL.Keycode.P => 80,
            SDL.Keycode.Q => 81,
            SDL.Keycode.R => 82,
            SDL.Keycode.S => 83,
            SDL.Keycode.T => 84,
            SDL.Keycode.U => 85,
            SDL.Keycode.V => 86,
            SDL.Keycode.W => 87,
            SDL.Keycode.X => 88,
            SDL.Keycode.Y => 89,
            SDL.Keycode.Z => 90,
            SDL.Keycode.Alpha0 => 48,
            SDL.Keycode.Alpha1 => 49,
            SDL.Keycode.Alpha2 => 50,
            SDL.Keycode.Alpha3 => 51,
            SDL.Keycode.Alpha4 => 52,
            SDL.Keycode.Alpha5 => 53,
            SDL.Keycode.Alpha6 => 54,
            SDL.Keycode.Alpha7 => 55,
            SDL.Keycode.Alpha8 => 56,
            SDL.Keycode.Alpha9 => 57,
            
            // Symbols
            SDL.Keycode.Space => 32,
            SDL.Keycode.Comma => 44,
            SDL.Keycode.Minus => 45,
            SDL.Keycode.Period => 46,
            SDL.Keycode.Slash => 47,
            SDL.Keycode.Semicolon => 59,
            SDL.Keycode.Equals => 61,
            SDL.Keycode.Apostrophe => 39,
            SDL.Keycode.LeftBracket => 91,
            SDL.Keycode.Backslash => 92,
            SDL.Keycode.RightBracket => 93,
            SDL.Keycode.Grave => 96,
            
            // Control / navigation
            SDL.Keycode.Return => 257,
            SDL.Keycode.Escape => 256,
            SDL.Keycode.Tab => 258,
            SDL.Keycode.Backspace => 259,
            SDL.Keycode.Insert => 260,
            SDL.Keycode.Delete => 261,
            SDL.Keycode.Right => 262,
            SDL.Keycode.Left => 263,
            SDL.Keycode.Down => 264,
            SDL.Keycode.Up => 265,
            SDL.Keycode.Pageup => 266,
            SDL.Keycode.Pagedown => 267,
            SDL.Keycode.Home => 268,
            SDL.Keycode.End => 269,
            SDL.Keycode.Capslock => 280,
            SDL.Keycode.ScrollLock => 281,
            SDL.Keycode.NumLockClear => 282,
            SDL.Keycode.PrintScreen => 283,
            SDL.Keycode.Pause => 284,
            
            // Function keys
            SDL.Keycode.F1 => 290,
            SDL.Keycode.F2 => 291,
            SDL.Keycode.F3 => 292,
            SDL.Keycode.F4 => 293,
            SDL.Keycode.F5 => 294,
            SDL.Keycode.F6 => 295,
            SDL.Keycode.F7 => 296,
            SDL.Keycode.F8 => 297,
            SDL.Keycode.F9 => 298,
            SDL.Keycode.F10 => 299,
            SDL.Keycode.F11 => 300,
            SDL.Keycode.F12 => 301,
            
            // Modifier keys
            SDL.Keycode.LShift => 340,
            SDL.Keycode.LCtrl => 341,
            SDL.Keycode.LAlt => 342,
            SDL.Keycode.LGUI => 343,
            SDL.Keycode.RShift => 344,
            SDL.Keycode.RCtrl => 345,
            SDL.Keycode.RAlt => 346,
            SDL.Keycode.RGUI => 347,
            SDL.Keycode.Menu => 348,
            
            // Keypad
            SDL.Keycode.Kp0 => 320,
            SDL.Keycode.Kp1 => 321,
            SDL.Keycode.Kp2 => 322,
            SDL.Keycode.Kp3 => 323,
            SDL.Keycode.Kp4 => 324,
            SDL.Keycode.Kp5 => 325,
            SDL.Keycode.Kp6 => 326,
            SDL.Keycode.Kp7 => 327,
            SDL.Keycode.Kp8 => 328,
            SDL.Keycode.Kp9 => 329,
            SDL.Keycode.KpPeriod => 330,
            SDL.Keycode.KpDivide => 331,
            SDL.Keycode.KpMultiply => 332,
            SDL.Keycode.KpMinus => 333,
            SDL.Keycode.KpPlus => 334,
            SDL.Keycode.KpEnter => 335,
            SDL.Keycode.KpEquals => 336,
            _ => 0
        };
    }
}