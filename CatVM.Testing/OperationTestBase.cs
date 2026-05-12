namespace CatVM.Testing;

public class OperationTestBase {
    protected CatVm _vm = null!;

    [SetUp]
    public void Setup() {
        _vm = new CatVm(512, 10_000);
    }

    protected void Execute(params byte[] data) {
        _vm.LoadData(data);
        _vm.Cpu.Ip = 0;
        _vm.ExecuteInstruction();
    }

    protected void ExecuteN(int times, params byte[] data) {
        _vm.LoadData(data);
        _vm.Cpu.Ip = 0;
        for (int i = 0; i < times; i++) {
            _vm.ExecuteInstruction();
        }
    }
}
