using System.Runtime.InteropServices;
using Raylib_cs;

namespace CatVM.Display.RaylibRenderer;

public class DisplayModeBuffer : IDisplayModeRenderer {
    private readonly Texture2D _texture;
    private readonly Shader _textureShader;
    
    public DisplayModeBuffer(CatVM vm) {
        if (((int)vm.DisplayMode & 0xf) != 0) {
            throw new NotImplementedException($"Display mode {vm.DisplayMode} not implemented!");
        }
        
        _textureShader = Raylib.LoadShaderFromMemory(null, RaylibRendering.BgrxShader);
        
        unsafe {
            byte[] blankImage = new byte[vm.DisplayWidth * vm.DisplayHeight * 4];
            GCHandle alloc = GCHandle.Alloc(blankImage, GCHandleType.Pinned);
                
            Image image = new() {
                Data = alloc.AddrOfPinnedObject().ToPointer(),
                Width = vm.DisplayWidth,
                Height = vm.DisplayHeight,
                Mipmaps = 1,
                Format = PixelFormat.UncompressedR8G8B8A8
            };
                
            _texture = Raylib.LoadTextureFromImage(image);
            
            // Raylib.UnloadImage(image);
            alloc.Free();
        }
    }

    public void Update(CatVM vm) { }
    
    public void ReadScreenData(CatVM vm) {
        unsafe {
            Raylib.UpdateTexture(_texture,
                (vm.MemoryHandle!.Value.AddrOfPinnedObject() + (nint)vm.DisplayBufferOffset).ToPointer());
        }
    }

    public void Draw(CatVM vm) {
        Raylib.BeginShaderMode(_textureShader);
        Raylib.DrawTexture(_texture, 0, 0, Color.White);
        Raylib.EndShaderMode();
    }

    public void Unload(CatVM vm) {
        Raylib.UnloadTexture(_texture);
    }
}
