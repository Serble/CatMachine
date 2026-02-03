using System.Numerics;

namespace IntegerMaths;

public static class MathsExtensions {
    private static readonly BigInteger UIntSize = new BigInteger(uint.MaxValue) + 1;

    public static uint ToUInt32WithOverflow(this BigInteger value) {
        return (uint)((value % UIntSize + UIntSize) % UIntSize);
    }
}
