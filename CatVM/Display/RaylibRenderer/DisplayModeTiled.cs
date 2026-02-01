using System.Numerics;
using Raylib_cs;

namespace CatVM.Display.RaylibRenderer;

public class DisplayModeTiled : IDisplayModeRenderer {
    private bool _uninitialised = true;
    private Color _clearColor;
    
    // tiles
    private readonly Shader _tileShader;
    private readonly Texture2D _palettes;
    private readonly Texture2D _images;
    private readonly Texture2D _tileImages;
    private readonly Texture2D _tilePalettes;
    
    // sprites
    private readonly Sprite[] _sprites = new Sprite[32];
    
    public DisplayModeTiled(CatVM vm) {
        if (((int)vm.DisplayMode & 0xf) != 1) {
            throw new NotImplementedException($"Display mode {vm.DisplayMode} not implemented!");
        }
        
        Console.WriteLine("hi");
        
        _tileShader = Raylib.LoadShaderFromMemory(
            RaylibRendering.ReadResource("CatVM.TileVertex.vert"),
            RaylibRendering.ReadResource("CatVM.TileFragment.frag")
        );

        _palettes = RaylibRendering.CreateTexture(8 * 16, 1,
            PixelFormat.UncompressedR8G8B8A8, 4);
        _images = RaylibRendering.CreateTexture(128, 256, // 1 row per image
            PixelFormat.UncompressedGrayscale, 1);
        _tileImages = RaylibRendering.CreateTexture(32*24, 1,
            PixelFormat.UncompressedGrayscale, 1);
        _tilePalettes = RaylibRendering.CreateTexture(32*24/2, 1,
            PixelFormat.UncompressedGrayscale, 1);
        
        SetShaderTexture(_tileShader, _palettes, "palettes");
        SetShaderTexture(_tileShader, _images, "images");
        SetShaderTexture(_tileShader, _tileImages, "tileImages");
        SetShaderTexture(_tileShader, _tilePalettes, "tilePalettes");
    }

    public void Unload(CatVM vm) {
        Raylib.UnloadShader(_tileShader);
        Raylib.UnloadTexture(_palettes);
        Raylib.UnloadTexture(_images);
        Raylib.UnloadTexture(_tileImages);
        Raylib.UnloadTexture(_tilePalettes);
    }

    public void ReadScreenData(CatVM vm) {
        // read all the data from the display buffer and store it so we can draw it later
        _uninitialised = false;
        uint pointer = vm.DisplayBufferOffset;
        
        _clearColor = RaylibRendering.BgrxToColor(vm.ReadWord(pointer));
        pointer += 4;

        UpdateTextureWithMemory(vm, _palettes, ref pointer, 8 * 16 * 4);
        UpdateTextureWithMemory(vm, _images, ref pointer, 256 * 128);
        UpdateTextureWithMemory(vm, _tileImages, ref pointer, 32 * 24);
        UpdateTextureWithMemory(vm, _tilePalettes, ref pointer, 32 * 24 / 2);

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
        
        foreach (Sprite sprite in _sprites) {
            if (sprite.DoDraw && sprite.DrawBehind) {
                sprite.Draw(this);
            }
        }

        Raylib.BeginShaderMode(_tileShader);
        Raylib.DrawRectangle(0, 0, vm.DisplayWidth, vm.DisplayHeight, new Color(0, 0, 0, 0));
        Raylib.EndShaderMode();

        foreach (Sprite sprite in _sprites) {
            if (sprite.DoDraw && !sprite.DrawBehind) {
                sprite.Draw(this);
            }
        }
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
            Raylib.DrawRectanglePro(
                new Rectangle(XPos, YPos, 16, 16),
                new Vector2(XPos, YPos),
                Rotation / (float)ushort.MaxValue,
                Color.White
            );
        }
    }
    
    private static void UpdateTextureWithMemory(CatVM vm, Texture2D texture, ref uint pointer, uint length) {
        Raylib.UpdateTexture(texture, vm.Memory.AsSpan((int)pointer..(int)(pointer + length)));
        pointer += length;
    }

    private static void SetShaderTexture(Shader shader, Texture2D texture, string uniformLocation) {
        int location = Raylib.GetShaderLocation(shader, uniformLocation);
        Raylib.SetShaderValueTexture(shader, location, texture);
    }
}
