namespace CatVM.Display;

public interface IRenderer {
    void Initialize(CatVM vm);
    Task Start(CatVM vm);
}
