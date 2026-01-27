using System.Runtime.CompilerServices;

namespace CatVM.Ops;

public static class NopOperation {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Nop(CatVM vm) {
        
    }
}
