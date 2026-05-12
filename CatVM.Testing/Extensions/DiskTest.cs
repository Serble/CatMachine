using CatVM.Extensions;

namespace CatVM.Testing.Extensions;

/// <summary>
/// Verifies the <see cref="Disk"/> extension: command protocol, read/write
/// against a backing <see cref="Stream"/>, queued (serialised) execution,
/// and the <see cref="SpecialInterrupts.DiskOperationFinish"/> interrupt.
/// </summary>
public class DiskTest {
    private const byte OpNop = 0x4D;
    private const int BlockSize = 512;

    /// <summary>
    /// Writes are persisted by a background task (drained from a channel),
    /// so tests that inspect the backing stream must wait for it.
    /// </summary>
    private const int WriteFlushTimeoutMs = 5_000;

    /// <summary>
    /// Disk callbacks are scheduled via <see cref="CatVm.RunIn"/> using a
    /// virtual-time clock when Fast=false. 1 cycle/sec ⇒ 1 second per cycle
    /// in picoseconds; we set picosPerBlock = PicosecondsPerCycle so each
    /// block takes exactly one NOP of virtual time.
    /// </summary>
    private static CatVm NewVm(int memory = 4096) {
        return new CatVm(memory, 1000) { Fast = false };
    }

    /// <summary>Picosecond cost of one NOP at 1000 cycles/sec.</summary>
    private static long OneCyclePs => CatVm.PicosecondsPerSecond / 1000;

    private static MemoryStream MakeBacking(int blocks, byte fill = 0) {
        byte[] data = new byte[blocks * BlockSize];
        if (fill != 0) Array.Fill(data, fill);
        // publiclyVisible:true so tests can call GetBuffer() to inspect/seed disk contents.
        return new MemoryStream(data, 0, data.Length, writable: true, publiclyVisible: true) { Position = 0 };
    }

    [Test]
    public void Probe_WriteZero_ReturnsType0x02() {
        CatVm vm = NewVm();
        using CancellationTokenSource cts = new();
        Disk disk = new(MakeBacking(1), OneCyclePs, token: cts.Token);
        disk.Output(vm, 0);
        Assert.That(disk.Input(vm), Is.EqualTo((uint)0x02));
    }

    [Test]
    public void Read_PopulatesVmMemoryFromStream() {
        CatVm vm = NewVm();
        MemoryStream backing = MakeBacking(2);

        // Put a recognisable pattern in block 1 of the disk.
        for (int i = 0; i < BlockSize; i++) backing.GetBuffer()[BlockSize + i] = (byte)(i ^ 0x5A);

        using CancellationTokenSource cts = new();
        Disk disk = new(backing, OneCyclePs, token: cts.Token);
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
        CatVm vm = NewVm();
        MemoryStream backing = MakeBacking(2);

        // Pattern in VM memory at 0x100, to be written into block 0.
        for (int i = 0; i < BlockSize; i++) vm.Memory[0x100 + i] = (byte)(i ^ 0xA5);

        using CancellationTokenSource cts = new();
        Disk disk = new(backing, OneCyclePs, token: cts.Token);
        disk.Output(vm, (uint)Disk.Mode.Write);
        disk.Output(vm, 0x100);
        disk.Output(vm, 0);  // startBlock
        disk.Output(vm, 1);  // count

        vm.LoadData(Enumerable.Repeat(OpNop, 4).ToArray());
        for (int i = 0; i < 4; i++) vm.ExecuteInstruction();

        // Writes are drained by a background task, so poll the backing
        // buffer until the last byte of the block matches (or we time out).
        byte[] buf = backing.GetBuffer();
        byte expectedLast = unchecked((byte)((BlockSize - 1) ^ 0xA5));
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(WriteFlushTimeoutMs);
        while (buf[BlockSize - 1] != expectedLast && DateTime.UtcNow < deadline) {
            Thread.Sleep(20);
        }

        for (int i = 0; i < BlockSize; i++) {
            Assert.That(buf[i], Is.EqualTo((byte)(i ^ 0xA5)),
                $"Disk byte {i} was not persisted");
        }
    }

    [Test]
    public void DiskOperationFinish_InterruptIsRaisedOnCompletion() {
        CatVm vm = NewVm();

        // Code at 0: NOPs. Handler at 0x800: NOP (sentinel address we look for).
        vm.LoadData(Enumerable.Repeat(OpNop, 8).ToArray());
        vm.LoadData([OpNop], 0x800);
        // IT layout: [u8 entryCount, (u8 id, u32 handler)*]
        byte handlerLo = 0x00, handlerMd = 0x08;
        vm.LoadData([
            1,
            (byte)SpecialInterrupts.DiskOperationFinish,
            handlerLo, handlerMd, 0x00, 0x00,
        ], 0x900);
        vm.Cpu.It = 0x900;
        vm.InterruptsEnabled = true;

        using CancellationTokenSource cts = new();
        Disk disk = new(MakeBacking(1), OneCyclePs, token: cts.Token);
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
        CatVm vm = NewVm();
        MemoryStream backing = MakeBacking(3);

        // Pre-populate block 0 and block 2 with distinct patterns so we can
        // verify both reads landed in the right spots in VM memory.
        for (int i = 0; i < BlockSize; i++) backing.GetBuffer()[i] = 0xAA;
        for (int i = 0; i < BlockSize; i++) backing.GetBuffer()[2 * BlockSize + i] = 0xBB;

        using CancellationTokenSource cts = new();
        Disk disk = new(backing, OneCyclePs, token: cts.Token);
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
}
