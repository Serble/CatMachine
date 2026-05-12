using System.Numerics;
using Raylib_cs;

namespace CatVM.Extensions.Renderer;

public class DisplayModeBuffer : IDisplayModeRenderer {
    private readonly Texture2D _texture;
    private readonly Shader _textureShader;
    
    public DisplayModeBuffer(RaylibPpu ppu) {
        if (((int)ppu.DisplayMode & 0xf) > 1) {
            throw new NotImplementedException($"Display mode {ppu.DisplayMode} not implemented!");
        }
        
        _textureShader = Raylib.LoadShaderFromMemory(null, BgrxShader);

        _texture = RaylibPpu.CreateTexture(ppu.DisplayWidth, ppu.DisplayHeight,
            PixelFormat.UncompressedR8G8B8A8, 4);
    }

    public void Update(RaylibPpu ppu, CatVm vm) { }
    
    public void ReadScreenData(RaylibPpu ppu, CatVm vm) {
        Raylib.UpdateTexture(_texture, vm.Memory.AsSpan((int)ppu.DisplayBufferAddress..));
    }

    public void Draw(RaylibPpu ppu, CatVm vm) {
        Raylib.ClearBackground(Color.Black);
        
        Raylib.BeginShaderMode(_textureShader);
        
        Rectangle source = ppu.GetCenteredBounds();
        Raylib.DrawTexturePro(_texture, source, new Rectangle(0, 0, ppu.DisplayWidth, ppu.DisplayHeight), Vector2.Zero, 0, Color.White);
        
        Raylib.EndShaderMode();
    }

    public void Unload(RaylibPpu ppu, CatVm vm) {
        Raylib.UnloadShader(_textureShader);
        Raylib.UnloadTexture(_texture);
    }
    
    private const string BgrxShader = 
"""
#version 330

in vec2 fragTexCoord;
out vec4 finalColor;
uniform sampler2D shaderData;

void main() {
    finalColor = vec4(texture(shaderData, fragTexCoord).bgr, 1.0);
}
""";
}
