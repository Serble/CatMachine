using System.Numerics;
using System.Runtime.InteropServices;
using SDL3;

namespace CatVM.Display.SdlRenderer;

public class DisplayModeTiled : IDisplayModeRenderer {
    private const int PaletteLocation = 4;
    private const int ImageLocation = PaletteLocation + 512;
    private const int TileScrollLocation = ImageLocation + 32768;
    private const int TileIndexLocation = TileScrollLocation + 2;
    private const int TilePaletteLocation = TileIndexLocation + 884;
    private const int SpriteLocation = TilePaletteLocation + 442;
    
    private readonly CatVM _vm;
    private readonly nint _device;
    private readonly nint _window;
    
    // GPU Resources
    private nint _tilePipeline;
    private nint _spritePipeline;
    private nint _sampler;
    private nint _displayTexture;
    
    private Color _clearColor;
    private bool _uninitialised = true;
    
    private readonly Sprite[] _sprites = new Sprite[32];
    private byte _scrollX;
    private byte _scrollY;

    // Uniform data structures to match shaders
    [StructLayout(LayoutKind.Sequential)]
    private struct FragUniforms {
        public int PaletteLoc;
        public int ImageLoc;
        public int TileScrollLoc;
        public int TileIndexLoc;
        public int TilePaletteLoc;
        public int SpriteLoc;
        public int ImageWidth;
        public float Padding; // Align to 16 bytes
    }

    public DisplayModeTiled(CatVM vm, nint window, nint renderer) {
        _vm = vm;
        _window = window;
        _device = SDL.GetGPURendererDevice(renderer);
        Console.WriteLine("hi " + _device);

        CreateGpuResources();
    }

    private void CreateGpuResources() {
        // 1. Create Sampler (Nearest neighbor for retro look)
        _sampler = SDL.CreateGPUSampler(_device, new SDL.GPUSamplerCreateInfo {
            MinFilter = SDL.GPUFilter.Nearest,
            MagFilter = SDL.GPUFilter.Nearest,
            MipmapMode = SDL.GPUSamplerMipmapMode.Nearest,
            AddressModeU = SDL.GPUSamplerAddressMode.ClampToEdge,
            AddressModeV = SDL.GPUSamplerAddressMode.ClampToEdge
        });

        // 2. Create the "Display Memory" Texture (177x196, R8_UNORM matches grayscale)
        _displayTexture = SDL.CreateGPUTexture(_device, new SDL.GPUTextureCreateInfo {
            Type = SDL.GPUTextureType.TextureType2D,
            Format = SDL.GPUTextureFormat.R8Unorm,
            Width = 177,
            Height = 196,
            LayerCountOrDepth = 1,
            NumLevels = 1,
            Usage = SDL.GPUTextureUsageFlags.Sampler// | SDL.GPUTextureUsageFlags.TransferSrc
        });

        // 3. Load Shaders (Assuming you have SPIR-V or platform-specific blobs)
        nint tileVert = SdlRendering.LoadShader("TileVertex.vert.spv", _device, SDL.GPUShaderStage.Vertex);
        nint tileFrag = SdlRendering.LoadShader("TileFragment.frag.spv", _device, SDL.GPUShaderStage.Fragment);
        // nint spriteVert = SdlRendering.LoadShader("SpriteVertex.vert.spv", _device, SDL.GPUShaderStage.Vertex);
        // nint spriteFrag = SdlRendering.LoadShader("SpriteFragment.frag.spv", _device, SDL.GPUShaderStage.Fragment);

        // 4. Create Pipelines
        _tilePipeline = CreatePipeline(tileVert, tileFrag, SDL.GPUPrimitiveType.TriangleList);
        // _spritePipeline = CreatePipeline(spriteVert, spriteFrag, SDL.GPUPrimitiveType.TriangleList, true);

        // Clean up shader modules after pipeline creation
        SDL.ReleaseGPUShader(_device, tileVert);
        SDL.ReleaseGPUShader(_device, tileFrag);
        // SDL.ReleaseGPUShader(_device, spriteVert);
        // SDL.ReleaseGPUShader(_device, spriteFrag);
    }

    private nint CreatePipeline(nint vert, nint frag, SDL.GPUPrimitiveType primitive, bool transparent = false) {
        SDL.GPUColorTargetDescription colorTarget = new() {
            Format = SDL.GetGPUSwapchainTextureFormat(_device, _window)
        };
        
        if (transparent) {
            colorTarget.BlendState.EnableBlend = true;
            colorTarget.BlendState.SrcColorBlendFactor = SDL.GPUBlendFactor.SrcAlpha;
            colorTarget.BlendState.DstColorBlendFactor = SDL.GPUBlendFactor.OneMinusSrcAlpha;
            colorTarget.BlendState.ColorBlendOp = SDL.GPUBlendOp.Add;
            colorTarget.BlendState.SrcAlphaBlendFactor = SDL.GPUBlendFactor.One;
            colorTarget.BlendState.DstAlphaBlendFactor = SDL.GPUBlendFactor.Zero;
            colorTarget.BlendState.AlphaBlendOp = SDL.GPUBlendOp.Add;
        }

        unsafe {
            SDL.GPUGraphicsPipelineCreateInfo info = new() {
                TargetInfo = new SDL.GPUGraphicsPipelineTargetInfo {
                    NumColorTargets = 1,
                    ColorTargetDescriptions = (nint)(&colorTarget)
                },
                PrimitiveType = primitive,
                VertexShader = vert,
                FragmentShader = frag
            };
            return SDL.CreateGPUGraphicsPipeline(_device, info);
        }
    }

    public void ReadScreenData() {
        _uninitialised = false;
        uint pointer = _vm.DisplayBufferAddress;
        
        // Clear Color
        uint clearVal = _vm.ReadWord(pointer);
        _clearColor = new Color((byte)(clearVal >> 16), (byte)(clearVal >> 8), (byte)clearVal, 255);

        // Update Texture via Transfer Buffer
        uint dataSize = (uint)_vm.DisplayBufferSize;
        nint transferBuffer = SDL.CreateGPUTransferBuffer(_device, new SDL.GPUTransferBufferCreateInfo {
            Size = dataSize,
            Usage = SDL.GPUTransferBufferUsage.Upload
        });

        unsafe {
            byte* map = (byte*)SDL.MapGPUTransferBuffer(_device, transferBuffer, false);
            _vm.Memory.AsSpan((int)pointer, (int)dataSize).CopyTo(new Span<byte>(map, (int)dataSize));
            SDL.UnmapGPUTransferBuffer(_device, transferBuffer);

            nint cmd = SDL.AcquireGPUCommandBuffer(_device);
            nint copyPass = SDL.BeginGPUCopyPass(cmd);
            SDL.UploadToGPUTexture(copyPass, new SDL.GPUTextureTransferInfo {
                TransferBuffer = transferBuffer,
                Offset = 0
            }, new SDL.GPUTextureRegion {
                Texture = _displayTexture,
                W = 177,
                H = 196,
                D = 1
            }, false);
            SDL.EndGPUCopyPass(copyPass);
            SDL.SubmitGPUCommandBuffer(cmd);
        }
        SDL.ReleaseGPUTransferBuffer(_device, transferBuffer);

        // Scroll and Sprites
        _scrollX = Math.Min(_vm.Memory[pointer + TileScrollLocation], (byte)32);
        _scrollY = Math.Min(_vm.Memory[pointer + TileScrollLocation + 1], (byte)32);
        
        pointer += SpriteLocation;
        for (int i = 0; i < _sprites.Length; i++) {
            byte img = _vm.Read8(pointer++);
            byte attr = _vm.Read8(pointer++);
            ushort x = _vm.Read16(pointer); pointer += 2;
            ushort y = _vm.Read16(pointer); pointer += 2;
            ushort rot = _vm.Read16(pointer); pointer += 2;

            _sprites[i] = new Sprite(img, (byte)(attr & 0x7), (attr & 0x10) != 0, 
                (attr & 0x20) != 0, (attr & 0x40) != 0, (attr & 0x80) != 0, x, y, rot);
        }
    }

    public void Draw() {
        nint cmd = SDL.AcquireGPUCommandBuffer(_device);
        if (cmd == nint.Zero) return;

        if (!SDL.AcquireGPUSwapchainTexture(cmd, _window, out nint swapTexture, out uint w, out uint h)) {
            SDL.SubmitGPUCommandBuffer(cmd);
            return;
        }

        SDL.GPUColorTargetInfo targetInfo = new() {
            Texture = swapTexture,
            ClearColor = new SDL.FColor { R = _clearColor.R/255f, G = _clearColor.G/255f, B = _clearColor.B/255f, A = 1.0f },
            LoadOp = SDL.GPULoadOp.Clear,
            StoreOp = SDL.GPUStoreOp.Store
        };

        unsafe {
            nint pass = SDL.BeginGPURenderPass(cmd, (nint)(&targetInfo), 1, nint.Zero);
            
            SDL.FRect dest = SdlRendering.GetCenteredBounds(_vm, _window);
            FragUniforms uniforms = new() {
                PaletteLoc = PaletteLocation, ImageLoc = ImageLocation,
                TileScrollLoc = TileScrollLocation, TileIndexLoc = TileIndexLocation,
                TilePaletteLoc = TilePaletteLocation, SpriteLoc = SpriteLocation,
                ImageWidth = 177
            };

            // 1. Draw "Behind" Sprites
            DrawSprites(_vm, pass, dest, uniforms, true);

            // 2. Draw Tiles
            SDL.BindGPUGraphicsPipeline(pass, _tilePipeline);
            BindCommonData(pass, uniforms);
            // Push bounds for TileVertex.vert
            SDL.PushGPUVertexUniformData(pass, 0, (nint)(&dest), 16); 
            SDL.DrawGPUPrimitives(pass, 6, 1, 0, 0);

            // 3. Draw "Front" Sprites
            DrawSprites(_vm, pass, dest, uniforms, false);

            SDL.EndGPURenderPass(pass);
        }

        SDL.SubmitGPUCommandBuffer(cmd);
    }

    private void BindCommonData(nint pass, FragUniforms uniforms) {
        unsafe {
            SDL.PushGPUFragmentUniformData(pass, 0, (nint)(&uniforms), (uint)Marshal.SizeOf<FragUniforms>());
            SDL.GPUTextureSamplerBinding binding = new() { Texture = _displayTexture, Sampler = _sampler };
            SDL.BindGPUFragmentSamplers(pass, 0, (nint)(&binding), 1);
        }
    }

    private void DrawSprites(CatVM vm, nint pass, SDL.FRect dest, FragUniforms uniforms, bool behind) {
        return;
        SDL.BindGPUGraphicsPipeline(pass, _spritePipeline);
        BindCommonData(pass, uniforms);

        foreach (var s in _sprites) {
            if (s.DoDraw && s.DrawBehind == behind) {
                // To keep it simple, we use PushConstants to pass sprite-specific data 
                // like X, Y, Rotation, and the Color-packed attributes to the vertex shader
                s.Draw(pass, vm, dest, _scrollX, _scrollY);
            }
        }
    }

    public void Unload() {
        SDL.ReleaseGPUGraphicsPipeline(_device, _tilePipeline);
        // SDL.ReleaseGPUGraphicsPipeline(_device, _spritePipeline);
        SDL.ReleaseGPUSampler(_device, _sampler);
        SDL.ReleaseGPUTexture(_device, _displayTexture);
    }

    private record Sprite(byte ImageIndex, byte Palette, bool HFlip, bool VFlip, bool DrawBehind, bool DoDraw, ushort XPos, ushort YPos, ushort Rotation) {
        public void Draw(nint pass, CatVM vm, SDL.FRect dest, byte scrollX, byte scrollY) {
            Vector2 displayRatio = new(dest.W / vm.DisplayWidth, dest.H / vm.DisplayHeight);
            Vector2 pos = new(XPos + 8 - scrollX, YPos + 8 - scrollY);
            pos = pos * displayRatio + new Vector2(dest.X, dest.Y);
            Vector2 size = new Vector2(16, 16) * displayRatio;

            // Packet for Vertex Shader (Position, Size, Rotation, Flip/Palette)
            float rotDeg = Rotation / (float)ushort.MaxValue * 360f;
            Vector4 spriteData = new(pos.X, pos.Y, size.X, size.Y);
            Vector4 spriteAttr = new(ImageIndex, HFlip ? 1 : 0, VFlip ? 1 : 0, Palette);

            unsafe {
                SDL.PushGPUVertexUniformData(pass, 0, (nint)(&spriteData), 16);
                SDL.PushGPUVertexUniformData(pass, 1, (nint)(&rotDeg), 4);
                SDL.PushGPUVertexUniformData(pass, 2, (nint)(&spriteAttr), 16);
                SDL.DrawGPUPrimitives(pass, 6, 1, 0, 0);
            }
        }
    }

    private struct Color(byte r, byte g, byte b, byte a) {
        public readonly byte R = r;
        public readonly byte G = g;
        public readonly byte B = b;
        public readonly byte A = a;
    }
}
