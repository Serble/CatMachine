namespace CatVM.Extensions.Renderer;

public interface IDisplayModeRenderer {
    public void ReadScreenData(RaylibPpu ppu, CatVM vm);
    public void Update(RaylibPpu ppu, CatVM vm);
    public void Draw(RaylibPpu ppu, CatVM vm);
    public void Unload(RaylibPpu ppu, CatVM vm);
}
