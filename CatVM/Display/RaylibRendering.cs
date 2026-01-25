using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Raylib_cs;

namespace CatVM.Display;

public class RaylibRendering : IRenderer {
    private readonly Queue<uint> _serialQueue = [];
    
    public void Initialize(CatVM vm) {
        
    }

    public Task Start(CatVM vm) {
        vm.SerialDevices.Add(0, (
            _ => _serialQueue.TryDequeue(out uint result) ? result : uint.MaxValue,
            (_, _) => {}
        ));
        
        return Task.Run((Action) (() => {
            unsafe {
                delegate* unmanaged[Cdecl]<int, sbyte*, sbyte*, void> ptr = &NopLogging;
                Raylib.SetTraceLogCallback(ptr);
            }
            
            Raylib.InitWindow(CatVM.DisplayWidth, CatVM.DisplayHeight, "CatVM Display");
            Raylib.SetTargetFPS(1024);

            if (!vm.MemoryHandle.HasValue) {
                throw new Exception("Memory not initialized.");
            }
            
            Shader shader = Raylib.LoadShaderFromMemory(null, 
                """
                      #version 330
                      
                      in vec2 fragTexCoord;
                      
                      out vec4 finalColor;
                      
                      uniform sampler2D shaderData;
                      
                      vec3 frag() {
                          return texture(shaderData, fragTexCoord).bgr;
                      }
                      
                      void main() {
                          finalColor = vec4(frag(), 1.0);
                      }
                      """);

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
            ManualResetEventSlim textureUpToDate = new(true);
            
            vm.UpdateDisplayEvent += () => {
                textureUpToDate.Reset(); // mark as outdated
                textureUpToDate.Wait();  // wait for it to be updated
            };

            while (true) {
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

                // if not up to date, update texture
                if (!textureUpToDate.IsSet) {
                    unsafe {
                        Raylib.UpdateTexture(texture,
                            (vm.MemoryHandle!.Value.AddrOfPinnedObject() + (int)vm.DisplayBufferOffset).ToPointer());
                    }

                    textureUpToDate.Set();
                }
                
                Raylib.BeginDrawing();
                
                Raylib.BeginShaderMode(shader);
                Raylib.DrawTexture(texture, 0, 0, Color.White);
                Raylib.EndShaderMode();
                
                Raylib.EndDrawing();
            }
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
}
