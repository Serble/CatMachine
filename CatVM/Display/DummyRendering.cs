namespace CatVM.Display;

public class DummyRendering : IRenderer {
    public void Initialize(CatVM vm) {
        
    }

    public Task Start(CatVM vm) {
        return Task.CompletedTask;
    }
}
