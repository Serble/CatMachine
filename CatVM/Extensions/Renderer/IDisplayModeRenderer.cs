namespace CatVM.Extensions.Renderer;

public interface IDisplayModeRenderer {
    public void ReadScreenData(RaylibPpu ppu, CatVm vm);
    public void Update(RaylibPpu ppu, CatVm vm);
    public void Draw(RaylibPpu ppu, CatVm vm);
    public void Unload(RaylibPpu ppu, CatVm vm);
}
