namespace CatVM.Ops;

public static class VirtModeRetOperation {

    public static void IRet(CatVM vm) {
        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Iret();
    }
}
