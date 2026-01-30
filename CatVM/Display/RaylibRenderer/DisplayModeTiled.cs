using System.Numerics;
using Raylib_cs;

namespace CatVM.Display.RaylibRenderer;

public class DisplayModeTiled : IDisplayModeRenderer {
    private const string PaletteShader =
"""
#version 330 core

in vec2 fragTexCoord;
in vec4 fragColor;

out vec4 finalColor;

uniform sampler2D texture0;
uniform vec4 colDiffuse;

uniform uint image[32];
uniform uint palette[16];

void main() {
    // finalColor = fragColor;
    // finalColor = vec4(vec2(int(gl_FragCoord.x) % 16, 16 - int(gl_FragCoord.y) % 16) / 16.0, 0.0, 1.0);
    // return;
    // uvec2 coord = uvec2(gl_FragCoord.x, 384 - gl_FragCoord.y); // /**/uvec2(0, 0);// /**/ uvec2(ivec2(fragTexCoord));
    uvec2 coord = uvec2(int(gl_FragCoord.x) % 16, 15 - int(gl_FragCoord.y) % 16);
    uint index = coord.x + coord.y * 16u; // position in color
    
    uint uIndex = index / 8u; // 2 colors per byte, 8 colors per uint
    uint colorIndex = image[uIndex];
    colorIndex = (colorIndex >> (((index / 2u) % 4u) * 8u)); // isolate byte
    
    if (index % 2u == 0u) { // get half byte
        colorIndex = (colorIndex >> 4) & 0xFu;
    } else {
        colorIndex = colorIndex & 0xFu;
    }
    
    finalColor = vec4(colorIndex, index % 2u, 0.0, 1.0);
    
    
    uint color = palette[colorIndex];
    finalColor = vec4(float((color >> 16u) & 0xffu) / 255.0, float((color >> 8u) & 0xffu) / 255.0,
        float(color & 0xffu) / 255.0, float((color >> 24u) & 0xffu) / 255.0);
    finalColor.a = 1.0;
    
    //finalColor = color;
}
""";

    private const string PaletteVertex =
"""
#version 330

// Input vertex attributes
in vec3 vertexPosition;
in vec2 vertexTexCoord;
in vec3 vertexNormal;
in vec4 vertexColor;

// Input uniform values
uniform mat4 mvp;

// Output vertex attributes (to fragment shader)
out vec2 fragTexCoord;
out vec4 fragColor;

// NOTE: Add your custom variables here

void main()
{
    // Send vertex attributes to fragment shader
    fragTexCoord = vertexTexCoord;
    fragColor = vertexColor;

    // Calculate final vertex position
    gl_Position = mvp*vec4(vertexPosition, 1.0);
}
""";
    
    private readonly Shader _paletteShader;
    private readonly int _paletteShaderImageLocation;
    private readonly int _paletteShaderPaletteLocation;
    
    private bool _uninitialised = true;
    private Color _clearColor;
    private readonly uint[][] _palettes = new uint[8][];
    private readonly byte[][] _images = new byte[256][];
    private readonly byte[] _tileIndexes = new byte[32 * 24];
    private readonly byte[] _tilePalettes = new byte[32 * 24 / 2];
    private readonly Sprite[] _sprites = new Sprite[32];
    
    public DisplayModeTiled(CatVM vm) {
        if (((int)vm.DisplayMode & 0xf) != 1) {
            throw new NotImplementedException($"Display mode {vm.DisplayMode} not implemented!");
        }
        
        Console.WriteLine("hi");
        
        _paletteShader = Raylib.LoadShaderFromMemory(null, PaletteShader);
        _paletteShaderImageLocation = Raylib.GetShaderLocation(_paletteShader, "image");
        _paletteShaderPaletteLocation = Raylib.GetShaderLocation(_paletteShader, "palette");

        for (int i = 0; i < _palettes.Length; i++) {
            _palettes[i] = new uint[16];
        }
        
        for (int i = 0; i < _images.Length; i++) {
            _images[i] = new byte[16 * 16 / 2]; // 16x16 image, 2 pixels per byte
        }
    }

    public void Unload(CatVM vm) {
        Raylib.UnloadShader(_paletteShader);
    }

    public void ReadScreenData(CatVM vm) {
        // read all the data from the display buffer and store it so we can draw it later
        _uninitialised = false;
        uint pointer = vm.DisplayBufferOffset;
        
        _clearColor = RaylibRendering.BgrxToColor(vm.ReadWord(pointer));
        pointer += 4;
        
        foreach (uint[] palette in _palettes) {
            for (int j = 0; j < palette.Length; j++) {
                palette[j] = vm.ReadWord(pointer);
                pointer += 4;
            }
        }

        for (int i = 0; i < _images.Length; i++) {
            _images[i] = vm.Memory.AsSpan((int)pointer..(int)(pointer + 128)).ToArray();
            pointer += 128; // 16×16 / 2
        }

        for (int i = 0; i < _tileIndexes.Length; i++) {
            _tileIndexes[i] = vm.Read8(pointer);
            pointer++;
        }

        for (int i = 0; i < _tilePalettes.Length; i++) {
            _tilePalettes[i] = vm.Read8(pointer);
            pointer++;
        }

        for (int i = 0; i < _sprites.Length; i++) {
            byte imageIndex = vm.Read8(pointer);
            pointer++;

            byte attributes = vm.Read8(pointer);
            pointer++;
            byte palette = (byte)(attributes & 0b1111);
            bool hFlip = ((attributes >> 4) & 0b1) != 0;
            bool vFlip = ((attributes >> 5) & 0b1) != 0;
            bool drawBehind = ((attributes >> 6) & 0b1) != 0;
            bool doDraw = ((attributes >> 7) & 0b1) != 0;

            ushort xPos = vm.Read16(pointer);
            pointer += 2;
            
            ushort yPos = vm.Read16(pointer);
            pointer += 2;
            
            ushort rotation = vm.Read16(pointer);
            pointer += 2;

            _sprites[i] = new Sprite(
                imageIndex,
                palette,
                hFlip,
                vFlip,
                drawBehind,
                doDraw,
                xPos,
                yPos,
                rotation
            );
        }
    }
    
    public void Update(CatVM vm) { }
    
    public void Draw(CatVM vm) {
        if (_uninitialised) {
            return;
        }
        
        Raylib.ClearBackground(_clearColor);
        
        Raylib.BeginShaderMode(_paletteShader);
        // Raylib.DrawRectangle(0, 0, vm.DisplayWidth, vm.DisplayHeight, Color.White);
        // Raylib.EndShaderMode();
        //
        // return;

        List<Sprite> foregroundSprites = [];
        foreach (Sprite sprite in _sprites) {
            if (!sprite.DoDraw) {
                continue;
            }

            if (!sprite.DrawBehind) {
                foregroundSprites.Add(sprite);
                continue;
            }

            sprite.Draw(this);
            Rlgl.DrawRenderBatchActive();
        }

        for (int i = 0; i < _tileIndexes.Length; i++) {
            byte[] image = _images[_tileIndexes[i]];
            Raylib.SetShaderValueV(_paletteShader, _paletteShaderImageLocation, image,
                ShaderUniformDataType.UInt, image.Length / 4);
            byte paletteIndex = _tilePalettes[i / 2];
            if (i % 2 == 0) {
                paletteIndex = (byte)((paletteIndex >> 4) & 0xf);
            }
            else {
                paletteIndex = (byte)(paletteIndex & 0xf);
            }

            uint[] palette = _palettes[paletteIndex];
            Raylib.SetShaderValueV(_paletteShader, _paletteShaderPaletteLocation, palette,
                ShaderUniformDataType.UInt, palette.Length);

            int x = i % 32 * 16;
            int y = i / 32 * 16;

            Raylib.DrawRectangle(x, y, 16, 16, Color.White);
            Rlgl.DrawRenderBatchActive();
        }

        foreach (Sprite sprite in foregroundSprites) {
            sprite.Draw(this);
            Rlgl.DrawRenderBatchActive();
        }
        
        Raylib.EndShaderMode();
    }

    private record Sprite(
        byte ImageIndex,
        byte Palette,
        bool HFlip, // TODO
        bool VFlip,
        bool DrawBehind,
        bool DoDraw,
        ushort XPos,
        ushort YPos,
        ushort Rotation
    ) {
        public void Draw(DisplayModeTiled dm) {
            Raylib.SetShaderValue(dm._paletteShader, dm._paletteShaderImageLocation, dm._images[ImageIndex], ShaderUniformDataType.UInt);
            Raylib.SetShaderValue(dm._paletteShader, dm._paletteShaderPaletteLocation, dm._palettes[Palette], ShaderUniformDataType.IVec4);
            
            Raylib.DrawRectanglePro(
                new Rectangle(XPos, YPos, 16, 16),
                new Vector2(XPos, YPos),
                Rotation / (float)ushort.MaxValue,
                Color.White
            );
        }
    }
}