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

        _texture = RaylibRendering.CreateTexture(vm.DisplayWidth, vm.DisplayHeight,
            PixelFormat.UncompressedR8G8B8A8, 4);
    }

    public void Update(CatVM vm) { }
    
    public void ReadScreenData(CatVM vm) {
        Raylib.UpdateTexture(_texture, vm.Memory.AsSpan((int)vm.DisplayBufferOffset..));
    }

    public void Draw(CatVM vm) {
        Raylib.BeginShaderMode(_textureShader);
        Raylib.DrawTexture(_texture, 0, 0, Color.White);
        Raylib.EndShaderMode();
    }

    public void Unload(CatVM vm) {
        Raylib.UnloadShader(_textureShader);
        Raylib.UnloadTexture(_texture);
    }
}
