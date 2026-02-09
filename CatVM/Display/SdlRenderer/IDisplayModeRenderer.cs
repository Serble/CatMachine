namespace CatVM.Display.SdlRenderer;

public interface IDisplayModeRenderer {
    public void ReadScreenData();
    public void Draw();
    public void Unload();
}
