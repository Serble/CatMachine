namespace CatVM.Ops;

public static class VirtModeRetOperation {

    public static void IRet(CatVm vm) {
        if (!vm.TryPrivileged()) {
            return;
        }

        vm.Iret();
    }
}
