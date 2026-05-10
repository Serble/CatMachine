namespace CatVM.Testing;

/// <summary>
/// Direct tests for <see cref="CatCpuState"/> — register get/set, flag bit helpers,
/// Mode bit isolation, RegRef mutation semantics, equality, and the
/// struct-layout invariant the Unsafe.Add-based register lookup depends on.
/// </summary>
public class CatCpuStateTest {

    [Test]
    public void Get_ValidRegisters_ReturnsField() {
        CatCpuState cpu = new() {
            R0 = 0x00, R1 = 0x11, R2 = 0x22, R3 = 0x33,
            R4 = 0x44, R5 = 0x55, R6 = 0x66, R7 = 0x77,
            Sp = 0x88, Ip = 0x99, Fl = 0xAA,
        };
        Assert.Multiple(() => {
            Assert.That(cpu.Get(0), Is.EqualTo(0x00u));
            Assert.That(cpu.Get(1), Is.EqualTo(0x11u));
            Assert.That(cpu.Get(2), Is.EqualTo(0x22u));
            Assert.That(cpu.Get(3), Is.EqualTo(0x33u));
            Assert.That(cpu.Get(4), Is.EqualTo(0x44u));
            Assert.That(cpu.Get(5), Is.EqualTo(0x55u));
            Assert.That(cpu.Get(6), Is.EqualTo(0x66u));
            Assert.That(cpu.Get(7), Is.EqualTo(0x77u));
            Assert.That(cpu.Get(8), Is.EqualTo(0x88u), "Sp at index 8");
            Assert.That(cpu.Get(9), Is.EqualTo(0x99u), "Ip at index 9");
            Assert.That(cpu.Get(10), Is.EqualTo(0xAAu), "Fl at index 10");
        });
    }

    [Test]
    public void Set_ValidRegisters_WritesField() {
        CatCpuState cpu = new();
        for (byte i = 0; i < 11; i++) cpu.Set(i, (uint)(i * 0x10));
        Assert.Multiple(() => {
            Assert.That(cpu.R0, Is.EqualTo(0x00u));
            Assert.That(cpu.R7, Is.EqualTo(0x70u));
            Assert.That(cpu.Sp, Is.EqualTo(0x80u));
            Assert.That(cpu.Ip, Is.EqualTo(0x90u));
            Assert.That(cpu.Fl, Is.EqualTo(0xA0u));
        });
    }

    [Test]
    public void Get_OutOfRange_Throws() {
        CatCpuState cpu = new();
        Assert.Multiple(() => {
            Assert.Throws<ArgumentOutOfRangeException>(() => cpu.Get(11));
            Assert.Throws<ArgumentOutOfRangeException>(() => cpu.Get(0xFF));
        });
    }

    [Test]
    public void Set_OutOfRange_Throws() {
        CatCpuState cpu = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => cpu.Set(11, 0));
    }

    [Test]
    public void RegRef_AllowsMutation() {
        CatCpuState cpu = new() { R3 = 5 };
        ref uint r = ref cpu.RegRef(3);
        r += 7;
        Assert.That(cpu.R3, Is.EqualTo(12u));
    }

    [Test]
    public void RegRef_OutOfRange_Throws() {
        CatCpuState cpu = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = cpu.RegRef(11); });
    }

    [Test]
    public void FlagBits_RoundTripIndependently() {
        CatCpuState cpu = new();

        // toggle each bit on, the others must remain off
        cpu.ZeroFlag = true;
        Assert.That(cpu.Fl, Is.EqualTo(0x01u));
        cpu.ZeroFlag = false;
        Assert.That(cpu.Fl, Is.EqualTo(0u));

        cpu.CarryFlag = true;
        Assert.That(cpu.Fl, Is.EqualTo(0x02u));

        cpu.SignFlag = true;
        Assert.That(cpu.Fl, Is.EqualTo(0x06u));

        cpu.OverflowFlag = true;
        Assert.That(cpu.Fl, Is.EqualTo(0x0Eu));

        cpu.CarryFlag = false;
        Assert.Multiple(() => {
            Assert.That(cpu.CarryFlag, Is.False);
            Assert.That(cpu.SignFlag, Is.True);
            Assert.That(cpu.OverflowFlag, Is.True);
            Assert.That(cpu.Fl, Is.EqualTo(0x0Cu));
        });
    }

    [Test]
    public void FlagBits_PreserveUnrelatedHighBits() {
        CatCpuState cpu = new() { Fl = 0xDEADBE00 };
        cpu.ZeroFlag = true;
        cpu.CarryFlag = true;
        cpu.SignFlag = true;
        cpu.OverflowFlag = true;
        Assert.That(cpu.Fl, Is.EqualTo(0xDEADBE0Fu));

        cpu.OverflowFlag = false;
        Assert.That(cpu.Fl, Is.EqualTo(0xDEADBE07u),
            "Clearing a flag must not modify other bits");
    }

    [Test]
    public void ModeBits_VirtualAndSupervisorAreIndependent() {
        CatCpuState cpu = new();
        cpu.VirtualMode = true;
        Assert.Multiple(() => {
            Assert.That(cpu.Mode, Is.EqualTo((byte)0b01));
            Assert.That(cpu.SupervisorMode, Is.False);
        });

        cpu.SupervisorMode = true;
        Assert.Multiple(() => {
            Assert.That(cpu.Mode, Is.EqualTo((byte)0b11));
            Assert.That(cpu.VirtualMode, Is.True);
        });

        cpu.VirtualMode = false;
        Assert.Multiple(() => {
            Assert.That(cpu.Mode, Is.EqualTo((byte)0b10));
            Assert.That(cpu.SupervisorMode, Is.True);
        });

        cpu.SupervisorMode = false;
        Assert.That(cpu.Mode, Is.EqualTo((byte)0));
    }

    [Test]
    public void ModeBits_HighBitsArePreserved() {
        CatCpuState cpu = new() { Mode = 0b11110000 };
        cpu.VirtualMode = true;
        Assert.That(cpu.Mode, Is.EqualTo((byte)0b11110001));
        cpu.VirtualMode = false;
        Assert.That(cpu.Mode, Is.EqualTo((byte)0b11110000));
    }

    [Test]
    public void Equals_True_WhenAllFieldsMatch() {
        CatCpuState a = new() {
            R0 = 1, R1 = 2, R3 = 3, Sp = 9, Ip = 4, Fl = 0xF,
            Mode = 0b01, It = 0x100, Ksp = 0x200, MBase = 0x300, MLen = 0x80,
        };
        CatCpuState b = a;
        Assert.Multiple(() => {
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.Equals((object)b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        });
    }

    [Test]
    public void Equals_False_WhenAnyFieldDiffers() {
        CatCpuState a = new() { R5 = 5 };
        CatCpuState b = new() { R5 = 6 };
        Assert.Multiple(() => {
            Assert.That(a.Equals(b), Is.False);
            Assert.That(a.Equals((object)"not a cpu"), Is.False);
            Assert.That(a.Equals((object?)null), Is.False);
        });
    }

    [Test]
    public void Dump_ContainsAllRegistersAndModeName() {
        CatCpuState cpu = new() { R0 = 0xAA, Mode = 0b01 };
        string s = cpu.Dump();
        Assert.Multiple(() => {
            Assert.That(s, Does.Contain("R0: 0x000000AA"));
            Assert.That(s, Does.Contain("User"));
            Assert.That(cpu.ToString(), Is.EqualTo(s));
        });
    }
}
