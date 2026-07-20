using System.Numerics;
using CatVM;
using Raylib_cs;

namespace RaylibPpuDevice;

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
    private readonly int _tileShaderBoundsLocation;
    
    private readonly Shader _spriteShader;
    private readonly Sprite[] _sprites = new Sprite[32];
    private byte _scrollX;
    private byte _scrollY;
    
    public DisplayModeTiled(RaylibPpu ppu) {
        if (((int)ppu.DisplayMode & 0xf) != 1) {
            throw new NotImplementedException($"Display mode {ppu.DisplayMode} not implemented!");
        }
        
        _tileShader = Raylib.LoadShaderFromMemory(
            RaylibPpu.ReadResource("TileVertex.vert"),
            RaylibPpu.ReadResource("TileFragment.frag")
        );

        _spriteShader = Raylib.LoadShaderFromMemory(
            null,
            RaylibPpu.ReadResource("SpriteFragment.frag")
        );
        
        _tileShaderBoundsLocation = Raylib.GetShaderLocation(_tileShader, "bounds");
        
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
        
        _displayData = RaylibPpu.CreateTexture(177, 196, // 177*196 == vm.DisplayBufferSize
            PixelFormat.UncompressedGrayscale, 1);
    }

    public void Unload(RaylibPpu ppu, CatVm vm) {
        Raylib.UnloadShader(_tileShader);
        Raylib.UnloadShader(_spriteShader);
        Raylib.UnloadTexture(_displayData);
    }

    public void ReadScreenData(RaylibPpu ppu, CatVm vm) {
        // read all the data from the display buffer and store it so we can draw it later
        _uninitialised = false;
        uint pointer = ppu.DisplayBufferAddress;
        
        _clearColor = RaylibPpu.BgrxToColor(vm.ReadWord(pointer));
        
        Raylib.UpdateTexture(_displayData, 
            vm.Memory.AsSpan((int)pointer..((int)pointer + ppu.DisplayBufferSize)));
        
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
    
    public void Update(RaylibPpu ppu, CatVm vm) { }
    
    public void Draw(RaylibPpu ppu, CatVm vm) {
        if (_uninitialised) {
            Raylib.ClearBackground(Color.Black);
            return;
        }
        
        Raylib.ClearBackground(_clearColor);
        
        Rectangle dest = ppu.GetCenteredBounds();
        
        Raylib.BeginShaderMode(_spriteShader);
        foreach (Sprite sprite in _sprites) {
            if (sprite.DoDraw && sprite.DrawBehind) {
                sprite.Draw(this, ppu, dest);
            }
        }
        Raylib.EndShaderMode();

        Raylib.BeginShaderMode(_tileShader);
        Raylib.SetShaderValue(_tileShader, _tileShaderBoundsLocation, dest, ShaderUniformDataType.Vec4);
        Raylib.DrawTexturePro(_displayData, new Rectangle(0, 0, 177, 196), 
            dest, Vector2.Zero, 0, Color.White);
        Raylib.EndShaderMode();

        Raylib.BeginShaderMode(_spriteShader);
        foreach (Sprite sprite in _sprites) {
            if (sprite.DoDraw && !sprite.DrawBehind) {
                sprite.Draw(this, ppu, dest);
            }
        }
        Raylib.EndShaderMode();
        
        // black bars (draw after everything so you cant see sprites outside the screen bounds)
        if (dest.X != 0) {
            Raylib.DrawRectangle(0, 0, (int)dest.X, Raylib.GetRenderHeight(), Color.Black);
            Raylib.DrawRectangle((int)(dest.X + dest.Width), 0, (int)dest.X + 100, Raylib.GetRenderHeight(), Color.Black);
        }
        else {
            Raylib.DrawRectangle(0, 0, Raylib.GetRenderWidth(), (int)dest.Y, Color.Black);
            Raylib.DrawRectangle(0, (int)(dest.Y + dest.Height), Raylib.GetRenderWidth(), (int)dest.Y + 100, Color.Black);
        }
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
        public void Draw(DisplayModeTiled dm, RaylibPpu ppu, Rectangle dest) {
            // Console.WriteLine($"{XPos} {YPos} {Rotation / (float)ushort.MaxValue * 360} {HFlip} {VFlip}");

            Vector2 displayRatio = dest.Size / new Vector2(ppu.DisplayWidth, ppu.DisplayHeight);
            
            Vector2 pos = new(XPos + 8 - dm._scrollX, YPos + 8 - dm._scrollY);
            pos = pos * displayRatio + dest.Position;
            
            Vector2 size = new Vector2(16, 16) * displayRatio;
            
            Raylib.DrawTexturePro(
                dm._displayData,
                new Rectangle(0, 0, 177, 196),
                new Rectangle(pos, size),
                size / 2f,
                Rotation / (float)ushort.MaxValue * 360,
                new Color(ImageIndex, (byte)(HFlip ? 255 : 0), (byte)(VFlip ? 255 : 0), Palette)
            );
        }
    }
}
