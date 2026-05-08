using CatVM.Extensions;

namespace CatVM.Testing.Extensions;

/// <summary>
/// Verifies the <see cref="Disk"/> extension: command protocol, read/write
/// against a backing <see cref="Stream"/>, queued (serialised) execution,
/// and the <see cref="SpecialInterupts.DiskOperationFinish"/> interrupt.
/// </summary>
public class DiskTest {
    private const byte OpNop = 0x4D;
    private const int BlockSize = 512;

    /// <summary>
    /// Disk callbacks are scheduled via <see cref="CatVM.RunIn"/> using a
    /// virtual-time clock when Fast=false. 1 cycle/sec ⇒ 1 second per cycle
    /// in picoseconds; we set picosPerBlock = PicosecondsPerCycle so each
    /// block takes exactly one NOP of virtual time.
    /// </summary>
    private static CatVM NewVm(int memory = 4096) {
        return new CatVM(memory, 1000) { Fast = false };
    }

    /// <summary>Picosecond cost of one NOP at 1000 cycles/sec.</summary>
    private static long OneCyclePs => CatVM.PicosecondsPerSecond / 1000;

    private static MemoryStream MakeBacking(int blocks, byte fill = 0) {
        byte[] data = new byte[blocks * BlockSize];
        if (fill != 0) Array.Fill(data, fill);
        // publiclyVisible:true so tests can call GetBuffer() to inspect/seed disk contents.
        return new MemoryStream(data, 0, data.Length, writable: true, publiclyVisible: true) { Position = 0 };
    }

    [Test]
    public void Probe_WriteZero_ReturnsType0x02() {
        CatVM vm = NewVm();
        using Disk disk = new(MakeBacking(1), OneCyclePs);
        disk.Output(vm, 0);
        Assert.That(disk.Input(vm), Is.EqualTo((uint)0x02));
    }

    [Test]
    public void Read_PopulatesVmMemoryFromStream() {
        CatVM vm = NewVm();
        MemoryStream backing = MakeBacking(2);

        // Put a recognisable pattern in block 1 of the disk.
        for (int i = 0; i < BlockSize; i++) backing.GetBuffer()[BlockSize + i] = (byte)(i ^ 0x5A);

        using Disk disk = new(backing, OneCyclePs);
        // Read mode (1): memAddr=0x200, startBlock=1, blockCount=1
        disk.Output(vm, (uint)Disk.Mode.Read);
        disk.Output(vm, 0x200);
        disk.Output(vm, 1);
        disk.Output(vm, 1);

        // Drive enough NOPs to let the scheduled IO callback fire.
        vm.LoadData(Enumerable.Repeat(OpNop, 4).ToArray());
        for (int i = 0; i < 4; i++) vm.ExecuteInstruction();

        for (int i = 0; i < BlockSize; i++) {
            Assert.That(vm.Memory[0x200 + i], Is.EqualTo((byte)(i ^ 0x5A)),
                $"Mismatch at offset {i}");
        }
    }

    [Test]
    public void Write_PersistsVmMemoryToStream() {
        CatVM vm = NewVm();
        MemoryStream backing = MakeBacking(2);

        // Pattern in VM memory at 0x100, to be written into block 0.
        for (int i = 0; i < BlockSize; i++) vm.Memory[0x100 + i] = (byte)(i ^ 0xA5);

        using Disk disk = new(backing, OneCyclePs);
        disk.Output(vm, (uint)Disk.Mode.Write);
        disk.Output(vm, 0x100);
        disk.Output(vm, 0);  // startBlock
        disk.Output(vm, 1);  // count

        vm.LoadData(Enumerable.Repeat(OpNop, 4).ToArray());
        for (int i = 0; i < 4; i++) vm.ExecuteInstruction();

        byte[] buf = backing.GetBuffer();
        for (int i = 0; i < BlockSize; i++) {
            Assert.That(buf[i], Is.EqualTo((byte)(i ^ 0xA5)),
                $"Disk byte {i} was not persisted");
        }
    }

    [Test]
    public void DiskOperationFinish_InterruptIsRaisedOnCompletion() {
        CatVM vm = NewVm();

        // Code at 0: NOPs. Handler at 0x800: NOP (sentinel address we look for).
        vm.LoadData(Enumerable.Repeat(OpNop, 8).ToArray());
        vm.LoadData([OpNop], 0x800);
        // IT layout: [u8 entryCount, (u8 id, u32 handler)*]
        byte handlerLo = 0x00, handlerMd = 0x08;
        vm.LoadData([
            1,
            (byte)SpecialInterupts.DiskOperationFinish,
            handlerLo, handlerMd, 0x00, 0x00,
        ], 0x900);
        vm.Cpu.It = 0x900;
        vm.InterruptsEnabled = true;

        using Disk disk = new(MakeBacking(1), OneCyclePs);
        disk.Output(vm, (uint)Disk.Mode.Read);
        disk.Output(vm, 0);
        disk.Output(vm, 0);
        disk.Output(vm, 1);

        for (int i = 0; i < 6; i++) vm.ExecuteInstruction();

        Assert.That(vm.Cpu.Ip, Is.GreaterThan(0x800u).And.LessThanOrEqualTo(0x810u),
            "Expected interrupt handler at 0x800 to have run after disk IO finished");
    }

    [Test]
    public void MultipleOperations_AreExecutedSerially() {
        CatVM vm = NewVm();
        MemoryStream backing = MakeBacking(3);

        // Pre-populate block 0 and block 2 with distinct patterns so we can
        // verify both reads landed in the right spots in VM memory.
        for (int i = 0; i < BlockSize; i++) backing.GetBuffer()[i] = 0xAA;
        for (int i = 0; i < BlockSize; i++) backing.GetBuffer()[2 * BlockSize + i] = 0xBB;

        using Disk disk = new(backing, OneCyclePs);
        disk.Output(vm, (uint)Disk.Mode.Read);
        disk.Output(vm, 0x200);
        disk.Output(vm, 0);
        disk.Output(vm, 1);

        disk.Output(vm, (uint)Disk.Mode.Read);
        disk.Output(vm, 0x600);
        disk.Output(vm, 2);
        disk.Output(vm, 1);

        // Need enough NOPs for both events to fire (each scheduled 1 cycle apart).
        vm.LoadData(Enumerable.Repeat(OpNop, 8).ToArray());
        for (int i = 0; i < 8; i++) vm.ExecuteInstruction();

        Assert.Multiple(() => {
            Assert.That(vm.Memory[0x200], Is.EqualTo((byte)0xAA),
                "First read should have populated 0x200");
            Assert.That(vm.Memory[0x600], Is.EqualTo((byte)0xBB),
                "Second read should have populated 0x600");
        });
    }

    [Test]
    public void Dispose_FlushesAndDisposesBackingStream() {
        CatVM vm = NewVm();
        MemoryStream backing = MakeBacking(1);
        Disk disk = new(backing, OneCyclePs);

        disk.Dispose();

        Assert.Throws<ObjectDisposedException>(() => _ = backing.Length,
            "Backing stream should be disposed after Disk.Dispose");
    }

    [Test]
    public async Task DisposeAsync_FlushesAndDisposesBackingStream() {
        CatVM vm = NewVm();
        MemoryStream backing = MakeBacking(1);
        Disk disk = new(backing, OneCyclePs);

        await disk.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => _ = backing.Length);
    }
}
