namespace CatVM.Testing;

public class MemoryOutOfRangeTest {

    [Test]
    public void Address_PreservedFromConstructor() {
        MemoryOutOfRange ex = new(true, 0xCAFEBABE, 4);
        Assert.That(ex.Address, Is.EqualTo(0xCAFEBABEu));
    }

    [Test]
    public void Message_DistinguishesReadFromWrite() {
        MemoryOutOfRange read  = new(false, 0x10, 1);
        MemoryOutOfRange write = new(true,  0x10, 1);
        Assert.Multiple(() => {
            Assert.That(read.Message,  Does.Contain("read"));
            Assert.That(write.Message, Does.Contain("write"));
        });
    }

    [Test]
    public void Message_IncludesAddressLengthAndDetail() {
        MemoryOutOfRange ex = new(false, 0x12345678, 8, "custom-detail");
        Assert.Multiple(() => {
            Assert.That(ex.Message, Does.Contain("0x12345678"));
            Assert.That(ex.Message, Does.Contain("8"));
            Assert.That(ex.Message, Does.Contain("custom-detail"));
        });
    }

    [Test]
    public void DefaultDetail_IsOutOfRange() {
        MemoryOutOfRange ex = new(false, 0, 1);
        Assert.That(ex.Message, Does.Contain("Out of range"));
    }
}
