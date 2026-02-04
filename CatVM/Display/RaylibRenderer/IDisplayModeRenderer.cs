namespace CatVM.Display.RaylibRenderer;

public interface IDisplayModeRenderer {
    public void ReadScreenData(CatVM vm);
    public void Update(CatVM vm);
    public void Draw(CatVM vm);
    public void Unload(CatVM vm);
}
