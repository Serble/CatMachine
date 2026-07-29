namespace CatVM.Testing.Ops;

/// <summary>
/// Verifies that every memory-operand instruction (all mov width variants and cpy)
/// routes its guest data accesses through the MMU when the CPU is in Virtual Mode.
/// <para/>
/// The window used throughout is <c>MBase = 0x1000</c>, <c>MLen = 0x400</c>. A guest
/// (virtual) address <c>V</c> therefore maps to physical <c>V + 0x1000</c> and must be
/// bounds-checked against <c>MLen</c>. Each test poisons the *untranslated* physical
/// location so that an instruction which forgot to translate would read/write the wrong
/// cell and fail the assertion.
/// </summary>
public class VirtualModeMemoryTest {
    private const uint Mbase = 0x1000;
    private const uint Mlen  = 0x400;

    // Guest (virtual) data address and its physical mapping.
    private const uint DataV = 0x80;
    private const uint DataP = Mbase + DataV;

    private const uint   Word    = 0x12345678;
    private const ushort Short   = 0x5678;
    private const byte   Byte8   = 0x78;

    private const uint   PoisonW = 0xDEADBEEF;
    private const ushort PoisonS = 0xDEAD;
    private const byte   PoisonB = 0xDE;

    private static readonly byte[] DataVLe =
        [(byte)(DataV & 0xFF), (byte)((DataV >> 8) & 0xFF), (byte)((DataV >> 16) & 0xFF), (byte)((DataV >> 24) & 0xFF)];

    /// <summary>
    /// Build a VM in virtual mode with <paramref name="instruction"/> placed at physical
    /// <see cref="Mbase"/> (i.e. guest IP 0).
    /// </summary>
    private static CatVm NewVirtVm(params byte[] instruction) {
        CatVm vm = new(64 * 1024, 100_000);
        vm.LoadData(instruction, Mbase);
        vm.Cpu.MBase = Mbase;
        vm.Cpu.MLen  = Mlen;
        vm.Cpu.Sp    = Mlen;
        vm.Cpu.Ip    = 0;
        vm.Cpu.Mode  = 0b01; // virtual mode, user
        return vm;
    }

    private static void PutWord(CatVm vm, uint physAddr, uint value) =>
        BitConverter.GetBytes(value).CopyTo(vm.Memory, (int)physAddr);

    private static void PutShort(CatVm vm, uint physAddr, ushort value) =>
        BitConverter.GetBytes(value).CopyTo(vm.Memory, (int)physAddr);

    private static uint GetWord(CatVm vm, uint physAddr) => BitConverter.ToUInt32(vm.Memory, (int)physAddr);
    private static ushort GetShort(CatVm vm, uint physAddr) => BitConverter.ToUInt16(vm.Memory, (int)physAddr);

    // ---------------------------------------------------------------------
    // 32-bit mov reads
    // ---------------------------------------------------------------------

    [Test]
    public void MovRRP_ReadTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x02, 0x02, 0x01); // MOV R2, [R1]
        vm.Cpu.R1 = DataV;
        PutWord(vm, DataP, Word);
        PutWord(vm, DataV, PoisonW); // poison the untranslated cell
        vm.ExecuteInstruction();
        Assert.That(vm.Cpu.R2, Is.EqualTo(Word));
        Assert.That(vm.Cpu.Ip, Is.EqualTo(3u));
    }

    [Test]
    public void MovRIP_ReadTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x03, 0x02, DataVLe[0], DataVLe[1], DataVLe[2], DataVLe[3]); // MOV R2, [DataV]
        PutWord(vm, DataP, Word);
        PutWord(vm, DataV, PoisonW);
        vm.ExecuteInstruction();
        Assert.That(vm.Cpu.R2, Is.EqualTo(Word));
        Assert.That(vm.Cpu.Ip, Is.EqualTo(6u));
    }

    // ---------------------------------------------------------------------
    // 32-bit mov writes
    // ---------------------------------------------------------------------

    [Test]
    public void MovRPR_WriteTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x04, 0x01, 0x02); // MOV [R1], R2
        vm.Cpu.R1 = DataV;
        vm.Cpu.R2 = Word;
        PutWord(vm, DataV, PoisonW);
        vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(GetWord(vm, DataP), Is.EqualTo(Word), "write hit the translated cell");
            Assert.That(GetWord(vm, DataV), Is.EqualTo(PoisonW), "untranslated cell untouched");
            Assert.That(vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }

    [Test]
    public void MovRPI_WriteTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x05, 0x01, 0x78, 0x56, 0x34, 0x12); // MOV [R1], 0x12345678
        vm.Cpu.R1 = DataV;
        PutWord(vm, DataV, PoisonW);
        vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(GetWord(vm, DataP), Is.EqualTo(Word));
            Assert.That(GetWord(vm, DataV), Is.EqualTo(PoisonW));
            Assert.That(vm.Cpu.Ip, Is.EqualTo(6u));
        });
    }

    [Test]
    public void MovIPR_WriteTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x06, DataVLe[0], DataVLe[1], DataVLe[2], DataVLe[3], 0x02); // MOV [DataV], R2
        vm.Cpu.R2 = Word;
        PutWord(vm, DataV, PoisonW);
        vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(GetWord(vm, DataP), Is.EqualTo(Word));
            Assert.That(GetWord(vm, DataV), Is.EqualTo(PoisonW));
            Assert.That(vm.Cpu.Ip, Is.EqualTo(6u));
        });
    }

    [Test]
    public void MovIPI_WriteTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x07, DataVLe[0], DataVLe[1], DataVLe[2], DataVLe[3], 0x78, 0x56, 0x34, 0x12); // MOV [DataV], 0x12345678
        PutWord(vm, DataV, PoisonW);
        vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(GetWord(vm, DataP), Is.EqualTo(Word));
            Assert.That(GetWord(vm, DataV), Is.EqualTo(PoisonW));
            Assert.That(vm.Cpu.Ip, Is.EqualTo(9u));
        });
    }

    // ---------------------------------------------------------------------
    // 16-bit mov (SMov)
    // ---------------------------------------------------------------------

    [Test]
    public void SMovRRP_ReadTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x08, 0x02, 0x01); // SMOV R2, [R1]
        vm.Cpu.R1 = DataV;
        PutShort(vm, DataP, Short);
        PutShort(vm, DataV, PoisonS);
        vm.ExecuteInstruction();
        Assert.That(vm.Cpu.R2, Is.EqualTo((uint)Short));
        Assert.That(vm.Cpu.Ip, Is.EqualTo(3u));
    }

    [Test]
    public void SMovRIP_ReadTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x09, 0x02, DataVLe[0], DataVLe[1], DataVLe[2], DataVLe[3]); // SMOV R2, [DataV]
        PutShort(vm, DataP, Short);
        PutShort(vm, DataV, PoisonS);
        vm.ExecuteInstruction();
        Assert.That(vm.Cpu.R2, Is.EqualTo((uint)Short));
        Assert.That(vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void SMovRPR_WriteTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x0A, 0x01, 0x02); // SMOV [R1], R2
        vm.Cpu.R1 = DataV;
        vm.Cpu.R2 = Word; // only low 16 bits stored
        PutShort(vm, DataV, PoisonS);
        vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(GetShort(vm, DataP), Is.EqualTo(Short));
            Assert.That(GetShort(vm, DataV), Is.EqualTo(PoisonS));
            Assert.That(vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }

    [Test]
    public void SMovRPI_WriteTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x0B, 0x01, 0x78, 0x56); // SMOV [R1], 0x5678
        vm.Cpu.R1 = DataV;
        PutShort(vm, DataV, PoisonS);
        vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(GetShort(vm, DataP), Is.EqualTo(Short));
            Assert.That(GetShort(vm, DataV), Is.EqualTo(PoisonS));
            Assert.That(vm.Cpu.Ip, Is.EqualTo(4u));
        });
    }

    [Test]
    public void SMovIPR_WriteTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x0C, DataVLe[0], DataVLe[1], DataVLe[2], DataVLe[3], 0x02); // SMOV [DataV], R2
        vm.Cpu.R2 = Word;
        PutShort(vm, DataV, PoisonS);
        vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(GetShort(vm, DataP), Is.EqualTo(Short));
            Assert.That(GetShort(vm, DataV), Is.EqualTo(PoisonS));
            Assert.That(vm.Cpu.Ip, Is.EqualTo(6u));
        });
    }

    [Test]
    public void SMovIPI_WriteTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x0D, DataVLe[0], DataVLe[1], DataVLe[2], DataVLe[3], 0x78, 0x56); // SMOV [DataV], 0x5678
        PutShort(vm, DataV, PoisonS);
        vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(GetShort(vm, DataP), Is.EqualTo(Short));
            Assert.That(GetShort(vm, DataV), Is.EqualTo(PoisonS));
            Assert.That(vm.Cpu.Ip, Is.EqualTo(7u));
        });
    }

    // ---------------------------------------------------------------------
    // 8-bit mov (BMov)
    // ---------------------------------------------------------------------

    [Test]
    public void BMovRRP_ReadTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x0E, 0x02, 0x01); // BMOV R2, [R1]
        vm.Cpu.R1 = DataV;
        vm.Memory[DataP] = Byte8;
        vm.Memory[DataV] = PoisonB;
        vm.ExecuteInstruction();
        Assert.That(vm.Cpu.R2, Is.EqualTo((uint)Byte8));
        Assert.That(vm.Cpu.Ip, Is.EqualTo(3u));
    }

    [Test]
    public void BMovRIP_ReadTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x0F, 0x02, DataVLe[0], DataVLe[1], DataVLe[2], DataVLe[3]); // BMOV R2, [DataV]
        vm.Memory[DataP] = Byte8;
        vm.Memory[DataV] = PoisonB;
        vm.ExecuteInstruction();
        Assert.That(vm.Cpu.R2, Is.EqualTo((uint)Byte8));
        Assert.That(vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void BMovRPR_WriteTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x10, 0x01, 0x02); // BMOV [R1], R2
        vm.Cpu.R1 = DataV;
        vm.Cpu.R2 = Word; // only low 8 bits stored
        vm.Memory[DataV] = PoisonB;
        vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(vm.Memory[DataP], Is.EqualTo(Byte8));
            Assert.That(vm.Memory[DataV], Is.EqualTo(PoisonB));
            Assert.That(vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }

    [Test]
    public void BMovRPI_WriteTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x11, 0x01, 0x78); // BMOV [R1], 0x78
        vm.Cpu.R1 = DataV;
        vm.Memory[DataV] = PoisonB;
        vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(vm.Memory[DataP], Is.EqualTo(Byte8));
            Assert.That(vm.Memory[DataV], Is.EqualTo(PoisonB));
            Assert.That(vm.Cpu.Ip, Is.EqualTo(3u));
        });
    }

    [Test]
    public void BMovIPR_WriteTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x12, DataVLe[0], DataVLe[1], DataVLe[2], DataVLe[3], 0x02); // BMOV [DataV], R2
        vm.Cpu.R2 = Word;
        vm.Memory[DataV] = PoisonB;
        vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(vm.Memory[DataP], Is.EqualTo(Byte8));
            Assert.That(vm.Memory[DataV], Is.EqualTo(PoisonB));
            Assert.That(vm.Cpu.Ip, Is.EqualTo(6u));
        });
    }

    [Test]
    public void BMovIPI_WriteTranslatesThroughMBase() {
        CatVm vm = NewVirtVm(0x13, DataVLe[0], DataVLe[1], DataVLe[2], DataVLe[3], 0x78); // BMOV [DataV], 0x78
        vm.Memory[DataV] = PoisonB;
        vm.ExecuteInstruction();
        Assert.Multiple(() => {
            Assert.That(vm.Memory[DataP], Is.EqualTo(Byte8));
            Assert.That(vm.Memory[DataV], Is.EqualTo(PoisonB));
            Assert.That(vm.Cpu.Ip, Is.EqualTo(6u));
        });
    }

    // ---------------------------------------------------------------------
    // cpy — both source and destination must translate
    // ---------------------------------------------------------------------

    private const uint SrcV  = 0x80;
    private const uint DstV  = 0xC0;
    private const uint SrcP  = Mbase + SrcV;
    private const uint DstP  = Mbase + DstV;
    private const uint CpyLen = 8;

    private static readonly byte[] SrcLe =
        [(byte)(SrcV & 0xFF), (byte)((SrcV >> 8) & 0xFF), (byte)((SrcV >> 16) & 0xFF), (byte)((SrcV >> 24) & 0xFF)];
    private static readonly byte[] LenLe =
        [(byte)(CpyLen & 0xFF), (byte)((CpyLen >> 8) & 0xFF), (byte)((CpyLen >> 16) & 0xFF), (byte)((CpyLen >> 24) & 0xFF)];

    private static void SeedCpy(CatVm vm) {
        for (uint i = 0; i < CpyLen; i++) {
            vm.Memory[SrcP + i] = (byte)(0xA0 + i); // real source data
            vm.Memory[SrcV + i] = 0x00;             // poison untranslated source
            vm.Memory[DstV + i] = PoisonB;          // poison untranslated dest
        }
    }

    private static void AssertCpyTranslated(CatVm vm) {
        Assert.Multiple(() => {
            for (uint i = 0; i < CpyLen; i++) {
                Assert.That(vm.Memory[DstP + i], Is.EqualTo((byte)(0xA0 + i)),
                    $"translated dest byte {i}");
                Assert.That(vm.Memory[DstV + i], Is.EqualTo(PoisonB),
                    $"untranslated dest byte {i} untouched");
            }
        });
    }

    [Test]
    public void CpyRR_BothEndsTranslate() {
        CatVm vm = NewVirtVm(0x41, 0x01, 0x02); // CPY [R1] -> R0, len R2
        vm.Cpu.R0 = DstV;
        vm.Cpu.R1 = SrcV;
        vm.Cpu.R2 = CpyLen;
        SeedCpy(vm);
        vm.ExecuteInstruction();
        AssertCpyTranslated(vm);
        Assert.That(vm.Cpu.Ip, Is.EqualTo(3u));
    }

    [Test]
    public void CpyRI_BothEndsTranslate() {
        CatVm vm = NewVirtVm(0x42, 0x01, LenLe[0], LenLe[1], LenLe[2], LenLe[3]); // CPY [R1] -> R0, len imm
        vm.Cpu.R0 = DstV;
        vm.Cpu.R1 = SrcV;
        SeedCpy(vm);
        vm.ExecuteInstruction();
        AssertCpyTranslated(vm);
        Assert.That(vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void CpyIR_BothEndsTranslate() {
        CatVm vm = NewVirtVm(0x43, SrcLe[0], SrcLe[1], SrcLe[2], SrcLe[3], 0x02); // CPY [imm] -> R0, len R2
        vm.Cpu.R0 = DstV;
        vm.Cpu.R2 = CpyLen;
        SeedCpy(vm);
        vm.ExecuteInstruction();
        AssertCpyTranslated(vm);
        Assert.That(vm.Cpu.Ip, Is.EqualTo(6u));
    }

    [Test]
    public void CpyII_BothEndsTranslate() {
        CatVm vm = NewVirtVm(0x44, SrcLe[0], SrcLe[1], SrcLe[2], SrcLe[3], LenLe[0], LenLe[1], LenLe[2], LenLe[3]); // CPY [imm] -> R0, len imm
        vm.Cpu.R0 = DstV;
        SeedCpy(vm);
        vm.ExecuteInstruction();
        AssertCpyTranslated(vm);
        Assert.That(vm.Cpu.Ip, Is.EqualTo(9u));
    }

    // ---------------------------------------------------------------------
    // Virtual-mode bounds enforcement (MLen) for memory operands
    // ---------------------------------------------------------------------

    [Test]
    public void MovIPI_WriteBeyondMLen_RaisesFault() {
        // Guest address exactly at MLen is out of range for any access width.
        byte[] a = BitConverter.GetBytes(Mlen);
        CatVm vm = NewVirtVm(0x07, a[0], a[1], a[2], a[3], 0x78, 0x56, 0x34, 0x12);
        vm.ExecuteWithErrorHandling(() => vm.ExecuteInstruction(fast: true));
        Assert.That(vm.Paused, Is.True);
    }

    [Test]
    public void MovRIP_ReadBeyondMLen_RaisesFault() {
        byte[] a = BitConverter.GetBytes(Mlen);
        CatVm vm = NewVirtVm(0x03, 0x02, a[0], a[1], a[2], a[3]);
        vm.ExecuteWithErrorHandling(() => vm.ExecuteInstruction(fast: true));
        Assert.That(vm.Paused, Is.True);
    }

    [Test]
    public void MovIPI_WriteStraddlingMLen_RaisesFault() {
        // Access starts in range but its 4-byte span overruns MLen.
        byte[] a = BitConverter.GetBytes(Mlen - 2);
        CatVm vm = NewVirtVm(0x07, a[0], a[1], a[2], a[3], 0x78, 0x56, 0x34, 0x12);
        vm.ExecuteWithErrorHandling(() => vm.ExecuteInstruction(fast: true));
        Assert.That(vm.Paused, Is.True);
    }

    [Test]
    public void CpyII_SourceBeyondMLen_RaisesFault() {
        byte[] src = BitConverter.GetBytes(Mlen); // source out of range
        CatVm vm = NewVirtVm(0x44, src[0], src[1], src[2], src[3], LenLe[0], LenLe[1], LenLe[2], LenLe[3]);
        vm.Cpu.R0 = DstV;
        vm.ExecuteWithErrorHandling(() => vm.ExecuteInstruction(fast: true));
        Assert.That(vm.Paused, Is.True);
    }

    [Test]
    public void CpyII_DestBeyondMLen_RaisesFault() {
        CatVm vm = NewVirtVm(0x44, SrcLe[0], SrcLe[1], SrcLe[2], SrcLe[3], LenLe[0], LenLe[1], LenLe[2], LenLe[3]);
        vm.Cpu.R0 = Mlen; // dest out of range
        SeedCpy(vm);
        vm.ExecuteWithErrorHandling(() => vm.ExecuteInstruction(fast: true));
        Assert.That(vm.Paused, Is.True);
    }
}
