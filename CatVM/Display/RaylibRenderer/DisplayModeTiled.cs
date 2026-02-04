using System.Numerics;
using Raylib_cs;

namespace CatVM.Display.RaylibRenderer;

public class DisplayModeTiled : IDisplayModeRenderer {
    private const int PaletteLocation = 4;
    private const int ImageLocation = PaletteLocation + 512;
    private const int TileScrollLocation = ImageLocation + 32768;
    private const int TileIndexLocation = TileScrollLocation + 2;
    private const int TilePaletteLocation = TileIndexLocation + 884;
    private const int SpriteLocation = TilePaletteLocation + 442;
    
    private bool _uninitialised = true;
    private Color _clearColor;
    
    private readonly Shader _tileShader;
    private readonly Texture2D _displayData;
    
    private readonly Shader _spriteShader;
    private readonly Sprite[] _sprites = new Sprite[32];
    private byte _scrollX;
    private byte _scrollY;
    
    public DisplayModeTiled(CatVM vm) {
        if (((int)vm.DisplayMode & 0xf) != 1) {
            throw new NotImplementedException($"Display mode {vm.DisplayMode} not implemented!");
        }
        
        _tileShader = Raylib.LoadShaderFromMemory(
            RaylibRendering.ReadResource("TileVertex.vert"),
            RaylibRendering.ReadResource("TileFragment.frag")
        );

        _spriteShader = Raylib.LoadShaderFromMemory(
            null,
            RaylibRendering.ReadResource("SpriteFragment.frag")
        );
        
        foreach (Shader shader in (Shader[])[_tileShader, _spriteShader]) {
            Raylib.SetShaderValue(shader, Raylib.GetShaderLocation(shader, "paletteLocation"), 
                PaletteLocation, ShaderUniformDataType.Int);
            Raylib.SetShaderValue(shader, Raylib.GetShaderLocation(shader, "imageLocation"), 
                ImageLocation, ShaderUniformDataType.Int);
            Raylib.SetShaderValue(shader, Raylib.GetShaderLocation(shader, "tileScrollLocation"), 
                TileScrollLocation, ShaderUniformDataType.Int);
            Raylib.SetShaderValue(shader, Raylib.GetShaderLocation(shader, "tileIndexLocation"), 
                TileIndexLocation, ShaderUniformDataType.Int);
            Raylib.SetShaderValue(shader, Raylib.GetShaderLocation(shader, "tilePaletteLocation"), 
                TilePaletteLocation, ShaderUniformDataType.Int);
            Raylib.SetShaderValue(shader, Raylib.GetShaderLocation(shader, "spriteLocation"), 
                SpriteLocation, ShaderUniformDataType.Int);
            Raylib.SetShaderValue(shader, Raylib.GetShaderLocation(shader, "imageWidth"), 
                177, ShaderUniformDataType.Int);
        }
        
        _displayData = RaylibRendering.CreateTexture(177, 196, // 177*196 == vm.DisplayBufferSize
            PixelFormat.UncompressedGrayscale, 1);
    }

    public void Unload(CatVM vm) {
        Raylib.UnloadShader(_tileShader);
        Raylib.UnloadShader(_spriteShader);
        Raylib.UnloadTexture(_displayData);
    }

    public void ReadScreenData(CatVM vm) {
        // read all the data from the display buffer and store it so we can draw it later
        _uninitialised = false;
        uint pointer = vm.DisplayBufferOffset;
        
        _clearColor = RaylibRendering.BgrxToColor(vm.ReadWord(pointer));
        
        Raylib.UpdateTexture(_displayData, 
            vm.Memory.AsSpan((int)pointer..((int)pointer + vm.DisplayBufferSize)));
        
        _scrollX = Math.Min(vm.Memory[pointer + TileScrollLocation], (byte)32);
        _scrollY = Math.Min(vm.Memory[pointer + TileScrollLocation + 1], (byte)32);
        
        pointer += SpriteLocation;

        for (int i = 0; i < _sprites.Length; i++) {
            byte imageIndex = vm.Read8(pointer);
            pointer++;

            byte attributes = vm.Read8(pointer);
            pointer++;
            byte palette = (byte)(attributes & 0b111);
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
            Raylib.ClearBackground(Color.Black);
            return;
        }
        
        Raylib.ClearBackground(_clearColor);
        
        Raylib.BeginShaderMode(_spriteShader);
        foreach (Sprite sprite in _sprites) {
            if (sprite.DoDraw && sprite.DrawBehind) {
                sprite.Draw(this);
            }
        }
        Raylib.EndShaderMode();

        Raylib.BeginShaderMode(_tileShader);
        Raylib.DrawTextureRec(_displayData, new Rectangle(0, 0, vm.DisplayWidth, vm.DisplayHeight), Vector2.Zero, Color.White);
        Raylib.EndShaderMode();

        Raylib.BeginShaderMode(_spriteShader);
        foreach (Sprite sprite in _sprites) {
            if (sprite.DoDraw && !sprite.DrawBehind) {
                sprite.Draw(this);
            }
        }
        Raylib.EndShaderMode();
    }

    private record Sprite(
        byte ImageIndex,
        byte Palette,
        bool HFlip,
        bool VFlip,
        bool DrawBehind,
        bool DoDraw,
        ushort XPos,
        ushort YPos,
        ushort Rotation
    ) {
        public void Draw(DisplayModeTiled dm) {
            // Console.WriteLine($"{XPos} {YPos} {Rotation / (float)ushort.MaxValue * 360} {HFlip} {VFlip}");
            Raylib.DrawTexturePro(
                dm._displayData,
                new Rectangle(0, 0, 177, 196),
                new Rectangle(XPos + 8 - dm._scrollX, YPos + 8 - dm._scrollY, 16, 16),
                new Vector2(8, 8),
                Rotation / (float)ushort.MaxValue * 360,
                new Color(ImageIndex, (byte)(HFlip ? 255 : 0), (byte)(VFlip ? 255 : 0), Palette)
            );
        }
    }
}
