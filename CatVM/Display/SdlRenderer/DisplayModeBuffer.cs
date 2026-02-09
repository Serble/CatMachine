using System.Runtime.InteropServices;
using SDL3;

namespace CatVM.Display.SdlRenderer;

public class DisplayModeBuffer : IDisplayModeRenderer {
    private readonly CatVM _vm;
    private readonly nint _window;
    private readonly nint _renderer;
    
    private readonly nint _texture;
    private readonly int _pitch;
    
    public DisplayModeBuffer(CatVM vm, nint window, nint renderer) {
        if (((int)vm.DisplayMode & 0xf) > 1) {
            throw new NotImplementedException($"Display mode {vm.DisplayMode} not implemented!");
        }
        
        _vm = vm;
        _window = window;
        _renderer = renderer;

        _pitch = vm.DisplayWidth * 4;
        _texture = SDL.CreateTexture(renderer, SDL.PixelFormat.XRGB8888, SDL.TextureAccess.Streaming, 
            vm.DisplayWidth, vm.DisplayHeight);
    }
    
    public void ReadScreenData() {
        unsafe {
            fixed (byte* memory = _vm.Memory) {
                nint pixels = new(memory + _vm.DisplayBufferAddress);
                
                SDL.UpdateTexture(
                    _texture,
                    nint.Zero, // update whole texture
                    pixels,
                    _pitch           // bytes per row
                );
            }            
        }
    }

    public void Draw() {
        SDL.SetRenderDrawColor(_renderer, 0, 0, 0, 255);
        SDL.RenderClear(_renderer);
        
        SDL.FRect dest = SdlRendering.GetCenteredBounds(_vm, _window);
        SDL.RenderTexture(_renderer, _texture, nint.Zero, dest);
        
        SDL.RenderPresent(_renderer);
    }

    public void Unload() {
        SDL.DestroyTexture(_texture);
    }
}
