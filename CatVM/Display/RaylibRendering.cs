using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace CatVM.Display;

public class RaylibRendering : IRenderer {
    public void Initialize(CatVM vm) {
        
    }

    public Task Start(CatVM vm) {
        return Task.Run((Action) (() => {
            unsafe {
                delegate* unmanaged[Cdecl]<int, sbyte*, sbyte*, void> ptr = &NopLogging;
                Raylib.SetTraceLogCallback(ptr);
            }
            
            Raylib.InitWindow(CatVM.DisplayWidth, CatVM.DisplayHeight, "CatVM Display");

            if (!vm.MemoryHandle.HasValue) {
                throw new Exception("Memory not initialized.");
            }

            Image image;
            unsafe {
                image = new Image {
                    Data = (vm.MemoryHandle!.Value.AddrOfPinnedObject() + (int)vm.DisplayBufferOffset).ToPointer(),
                    Width = CatVM.DisplayWidth,
                    Height = CatVM.DisplayHeight,
                    Mipmaps = 1,
                    Format = PixelFormat.UncompressedR8G8B8A8
                };
            }
            
            Texture2D texture = Raylib.LoadTextureFromImage(image);

            HashSet<KeyboardKey> pressedKeys = [];

            while (true) {
                unsafe {
                    if (Raylib.WindowShouldClose()) {
                        // close window
                        Raylib.CloseWindow();
                        Environment.Exit(0);
                    }

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

                    Raylib.UpdateTexture(texture, (vm.MemoryHandle!.Value.AddrOfPinnedObject() + (int)vm.DisplayBufferOffset).ToPointer());
        
                    Raylib.BeginDrawing();
                    Raylib.ClearBackground(Color.Black);

                    Raylib.DrawTexture(texture, 0, 0, Color.White);
        
                    Raylib.EndDrawing();
                }
            }
        }));
    }

    private static void SendInput(CatVM vm, uint device, uint inputType, uint value) {
        vm.StackPush(vm.Cpu.R1);
        vm.StackPush(vm.Cpu.R2);
        vm.StackPush(vm.Cpu.R3);
        vm.Cpu.R1 = device;
        vm.Cpu.R2 = inputType;
        vm.Cpu.R3 = value;
        vm.Interrupt(SpecialInterupts.HandleInput);
        vm.Cpu.R3 = vm.StackPop();
        vm.Cpu.R2 = vm.StackPop();
        vm.Cpu.R1 = vm.StackPop();
    }
    
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    public static unsafe void NopLogging(int logLevel, sbyte* msg, sbyte* args) {
        
    }
}
