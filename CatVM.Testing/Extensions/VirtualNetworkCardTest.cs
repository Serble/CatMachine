using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CatVM.Extensions;

namespace CatVM.Testing.Extensions;

/// <summary>
/// Verifies <see cref="VirtualNetworkCard"/> end-to-end behaviour over real
/// loopback UDP sockets, plus its descriptor / status state machine. Each
/// test creates ephemeral sockets, so they're independent and parallel-safe.
/// </summary>
public class VirtualNetworkCardTest {
    private const uint DescriptorSize = 9;            // bufAddr(4) + bufLen(4) + flags(1)
    private const byte DescOwn = 0x01;
    private const byte DescEndOfPacket = 0x02;
    private const byte DescDone = 0x04;

    private const uint StatusReceiveDone = 0x01;
    private const uint StatusTransmitDone = 0x02;
    private const uint StatusPacketDropped = 0x04;
    private const uint StatusLinkUp = 0x08;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private static int GetFreePort() {
        // OS picks a free ephemeral port; we close immediately and reuse it.
        // Small race window, but acceptable for tests on a developer machine
        // and CI runners are isolated per-job.
        using UdpClient probe = new(0);
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    private static CatVm NewVm(int memory = 4096) {
        return new CatVm(memory, 10_000) { Fast = true };
    }

    private static void WriteU32(byte[] mem, uint addr, uint value) {
        mem[addr]     = (byte)(value & 0xFF);
        mem[addr + 1] = (byte)((value >> 8) & 0xFF);
        mem[addr + 2] = (byte)((value >> 16) & 0xFF);
        mem[addr + 3] = (byte)((value >> 24) & 0xFF);
    }

    private static byte[] MakeFrame(byte[] dstMac, byte[] srcMac, string payload) {
        byte[] body = Encoding.ASCII.GetBytes(payload);
        byte[] frame = new byte[14 + body.Length];
        Array.Copy(dstMac, 0, frame, 0, 6);
        Array.Copy(srcMac, 0, frame, 6, 6);
        // ethertype left as 0x0000
        Array.Copy(body, 0, frame, 14, body.Length);
        return frame;
    }

    private static byte[] BroadcastMac() => [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

    private static void WaitFor(Func<bool> condition, string desc) {
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.Elapsed < Timeout) {
            if (condition()) return;
            Thread.Sleep(5);
        }
        Assert.Fail($"Timed out waiting for: {desc}");
    }

    /// <summary>
    /// Standard two-port setup: the VNIC binds to <paramref name="vnicPort"/>
    /// and is connected to <paramref name="peerPort"/>. The returned
    /// <see cref="UdpClient"/> can be used to send packets to the VNIC's
    /// listener thread (the VNIC will respond/transmit to the peer port).
    /// </summary>
    private static (VirtualNetworkCard vnic, UdpClient peer) CreatePair(CatVm vm, out int vnicPort, out int peerPort) {
        vnicPort = GetFreePort();
        peerPort = GetFreePort();
        UdpClient peer = new(peerPort);
        peer.Connect(IPAddress.Loopback, vnicPort);
        VirtualNetworkCard vnic = new(vm, new IPEndPoint(IPAddress.Loopback, peerPort), vnicPort);
        return (vnic, peer);
    }

    [Test]
    public void Probe_WriteZero_ReturnsType0x04() {
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            vnic.Output(vm, 0);
            Assert.That(vnic.Input(vm), Is.EqualTo((uint)0x29FEF534));
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }

    [Test]
    public void Tx_BroadcastFrame_IsTransmittedAndDescriptorMarkedDone() {
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            uint frameAddr = 0x200;
            uint descAddr = 0x100;
            byte[] frame = MakeFrame(BroadcastMac(), [1, 2, 3, 4, 5, 6], "hello-tx");
            Array.Copy(frame, 0, vm.Memory, frameAddr, frame.Length);

            WriteU32(vm.Memory, descAddr, frameAddr);
            WriteU32(vm.Memory, descAddr + 4, (uint)frame.Length);
            vm.Memory[descAddr + 8] = DescOwn;

            // SetTxRing(addr, length=1)
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetTxRing);
            vnic.Output(vm, descAddr);
            vnic.Output(vm, 1);

            // KickTx
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.KickTx);

            byte[] received = null!;
            WaitFor(() => {
                if (peer.Available == 0) return false;
                IPEndPoint ep = new(IPAddress.Any, 0);
                received = peer.Receive(ref ep);
                return true;
            }, "UDP packet from VNIC");

            Assert.That(received, Is.EqualTo(frame));

            WaitFor(
                () => (vm.Memory[descAddr + 8] & DescDone) != 0
                   && (vm.Memory[descAddr + 8] & DescOwn) == 0,
                "TX descriptor flagged Done with Own cleared");
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }

    [Test]
    public void Tx_DescriptorWithoutOwn_IsNotTransmitted() {
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            uint frameAddr = 0x200;
            uint descAddr = 0x100;
            byte[] frame = MakeFrame(BroadcastMac(), [0, 0, 0, 0, 0, 0], "should-not-send");
            Array.Copy(frame, 0, vm.Memory, frameAddr, frame.Length);

            WriteU32(vm.Memory, descAddr, frameAddr);
            WriteU32(vm.Memory, descAddr + 4, (uint)frame.Length);
            vm.Memory[descAddr + 8] = 0;  // not Own ⇒ guest still owns

            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetTxRing);
            vnic.Output(vm, descAddr);
            vnic.Output(vm, 1);
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.KickTx);

            // Brief wait — no packet should ever arrive.
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 100) {
                Assert.That(peer.Available, Is.Zero,
                    "VNIC must not transmit a descriptor without the Own flag");
                Thread.Sleep(10);
            }
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }

    [Test]
    public void Tx_SnapshotsBufferBeforeTransmit() {
        // Verifies that mutating the TX buffer immediately after KickTx does
        // not affect what gets sent on the wire (the VNIC takes a snapshot).
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            uint frameAddr = 0x200;
            uint descAddr = 0x100;
            byte[] frame = MakeFrame(BroadcastMac(), [1, 2, 3, 4, 5, 6], "original");
            Array.Copy(frame, 0, vm.Memory, frameAddr, frame.Length);
            byte[] expected = (byte[])frame.Clone();

            WriteU32(vm.Memory, descAddr, frameAddr);
            WriteU32(vm.Memory, descAddr + 4, (uint)frame.Length);
            vm.Memory[descAddr + 8] = DescOwn;

            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetTxRing);
            vnic.Output(vm, descAddr);
            vnic.Output(vm, 1);
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.KickTx);

            // Immediately scribble over the buffer.
            for (int i = 14; i < frame.Length; i++) vm.Memory[frameAddr + i] = 0xEE;

            byte[] received = null!;
            WaitFor(() => {
                if (peer.Available == 0) return false;
                IPEndPoint ep = new(IPAddress.Any, 0);
                received = peer.Receive(ref ep);
                return true;
            }, "UDP packet from VNIC");

            Assert.That(received, Is.EqualTo(expected),
                "Snapshot must capture the buffer at KickTx time");
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }

    [Test]
    public void Rx_BroadcastFrame_PopulatesDescriptorAndSetsDone() {
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            uint bufAddr = 0x300;
            uint descAddr = 0x100;
            uint bufLen = 256;
            WriteU32(vm.Memory, descAddr, bufAddr);
            WriteU32(vm.Memory, descAddr + 4, bufLen);
            vm.Memory[descAddr + 8] = 0;

            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetRxRing);
            vnic.Output(vm, descAddr);
            vnic.Output(vm, 1);

            byte[] frame = MakeFrame(BroadcastMac(), [9, 8, 7, 6, 5, 4], "rx-broadcast");
            peer.Send(frame, frame.Length);

            WaitFor(() => (vm.Memory[descAddr + 8] & DescDone) != 0,
                "RX descriptor flagged Done");

            byte[] copied = new byte[frame.Length];
            Array.Copy(vm.Memory, bufAddr, copied, 0, frame.Length);
            Assert.Multiple(() => {
                Assert.That(copied, Is.EqualTo(frame));
                Assert.That(vm.Memory[descAddr + 8] & DescEndOfPacket, Is.Not.Zero,
                    "EndOfPacket should also be set");
            });
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }

    [Test]
    public void Rx_FrameToDifferentMac_IsDropped() {
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            // SetMac to AA:BB:CC:DD:EE:FF (LE-packed across two args)
            // args[0] = 0xDDCCBBAA, args[1] = 0xFFEE
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetMac);
            vnic.Output(vm, 0xDDCCBBAA);
            vnic.Output(vm, 0x0000FFEE);

            uint bufAddr = 0x300;
            uint descAddr = 0x100;
            WriteU32(vm.Memory, descAddr, bufAddr);
            WriteU32(vm.Memory, descAddr + 4, 256);
            vm.Memory[descAddr + 8] = 0;

            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetRxRing);
            vnic.Output(vm, descAddr);
            vnic.Output(vm, 1);

            // Send a unicast frame to a different MAC.
            byte[] frame = MakeFrame([0x11, 0x22, 0x33, 0x44, 0x55, 0x66], [0, 0, 0, 0, 0, 0], "wrong-mac");
            peer.Send(frame, frame.Length);

            // Confirm descriptor never gets Done set.
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 200) {
                Assert.That(vm.Memory[descAddr + 8] & DescDone, Is.Zero,
                    "Frame to wrong MAC should be dropped");
                Thread.Sleep(10);
            }
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }

    [Test]
    public void Rx_FrameToConfiguredMac_IsAccepted() {
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            byte[] mac = [0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF];
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetMac);
            vnic.Output(vm, 0xDDCCBBAA);
            vnic.Output(vm, 0x0000FFEE);

            uint bufAddr = 0x300;
            uint descAddr = 0x100;
            WriteU32(vm.Memory, descAddr, bufAddr);
            WriteU32(vm.Memory, descAddr + 4, 256);
            vm.Memory[descAddr + 8] = 0;

            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetRxRing);
            vnic.Output(vm, descAddr);
            vnic.Output(vm, 1);

            byte[] frame = MakeFrame(mac, [1, 2, 3, 4, 5, 6], "for-me");
            peer.Send(frame, frame.Length);

            WaitFor(() => (vm.Memory[descAddr + 8] & DescDone) != 0,
                "RX descriptor flagged Done for matching MAC");
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }

    [Test]
    public void Rx_ShortFrame_IsDropped() {
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            uint descAddr = 0x100;
            WriteU32(vm.Memory, descAddr, 0x300);
            WriteU32(vm.Memory, descAddr + 4, 256);
            vm.Memory[descAddr + 8] = 0;

            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetRxRing);
            vnic.Output(vm, descAddr);
            vnic.Output(vm, 1);

            // Less than 14 bytes ⇒ dropped before any descriptor work.
            peer.Send([1, 2, 3, 4, 5], 5);

            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 200) {
                Assert.That(vm.Memory[descAddr + 8] & DescDone, Is.Zero,
                    "Short frame must not populate any descriptor");
                Thread.Sleep(10);
            }
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }

    [Test]
    public void Rx_RingFull_RaisesPacketDroppedStatus() {
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            // Single descriptor; we'll fill it then send another packet.
            uint descAddr = 0x100;
            WriteU32(vm.Memory, descAddr, 0x300);
            WriteU32(vm.Memory, descAddr + 4, 256);
            vm.Memory[descAddr + 8] = 0;

            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetRxRing);
            vnic.Output(vm, descAddr);
            vnic.Output(vm, 1);

            byte[] frame = MakeFrame(BroadcastMac(), [1, 2, 3, 4, 5, 6], "first");
            peer.Send(frame, frame.Length);
            WaitFor(() => (vm.Memory[descAddr + 8] & DescDone) != 0, "first packet placed");

            // Drain the ReceiveDone status (and clear it) so we're left with a
            // clean slate.
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.GetStatus);
            _ = vnic.Input(vm);

            // Now the only descriptor still has Done set ⇒ ring is "full".
            byte[] frame2 = MakeFrame(BroadcastMac(), [1, 2, 3, 4, 5, 6], "overflow");
            peer.Send(frame2, frame2.Length);

            WaitFor(() => {
                vnic.Output(vm, (uint)VirtualNetworkCard.Mode.GetStatus);
                uint status = vnic.Input(vm);
                return (status & StatusPacketDropped) != 0;
            }, "PacketDropped status raised when no descriptor available");
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }

    [Test]
    public void GetStatus_ReadsAndClearsExceptLinkUp() {
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            // Send a TX so we'll end up with TransmitDone set.
            uint frameAddr = 0x200;
            uint descAddr = 0x100;
            byte[] frame = MakeFrame(BroadcastMac(), [1, 2, 3, 4, 5, 6], "status");
            Array.Copy(frame, 0, vm.Memory, frameAddr, frame.Length);
            WriteU32(vm.Memory, descAddr, frameAddr);
            WriteU32(vm.Memory, descAddr + 4, (uint)frame.Length);
            vm.Memory[descAddr + 8] = DescOwn;

            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetTxRing);
            vnic.Output(vm, descAddr);
            vnic.Output(vm, 1);
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.KickTx);

            uint status = 0;
            WaitFor(() => {
                vnic.Output(vm, (uint)VirtualNetworkCard.Mode.GetStatus);
                status = vnic.Input(vm);
                return (status & StatusTransmitDone) != 0;
            }, "TransmitDone visible in status");

            Assert.That(status & StatusLinkUp, Is.Not.Zero,
                "LinkUp should always be reported");

            // Second read: only LinkUp should remain (TransmitDone was cleared).
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.GetStatus);
            uint status2 = vnic.Input(vm);
            Assert.That(status2, Is.EqualTo(StatusLinkUp),
                "Status flags should be cleared on read except LinkUp");
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }

    [Test]
    public void Reset_ClearsRingsAndStateSoFurtherTrafficIsIgnored() {
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            uint descAddr = 0x100;
            WriteU32(vm.Memory, descAddr, 0x300);
            WriteU32(vm.Memory, descAddr + 4, 256);
            vm.Memory[descAddr + 8] = 0;

            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetRxRing);
            vnic.Output(vm, descAddr);
            vnic.Output(vm, 1);

            // Reset
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.Reset);

            // Sending after a reset should not populate the (now-disconnected) descriptor.
            byte[] frame = MakeFrame(BroadcastMac(), [1, 2, 3, 4, 5, 6], "post-reset");
            peer.Send(frame, frame.Length);

            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 200) {
                Assert.That(vm.Memory[descAddr + 8] & DescDone, Is.Zero,
                    "Reset must detach the RX ring so old descriptors are not touched");
                Thread.Sleep(10);
            }
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }

    [Test]
    public void Tx_BufferBeyondMemory_IsSilentlySkipped() {
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            uint descAddr = 0x100;
            // Buffer extending past the end of memory.
            WriteU32(vm.Memory, descAddr, (uint)(vm.Memory.Length - 8));
            WriteU32(vm.Memory, descAddr + 4, 1024);
            vm.Memory[descAddr + 8] = DescOwn;

            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetTxRing);
            vnic.Output(vm, descAddr);
            vnic.Output(vm, 1);
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.KickTx);

            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 100) {
                Assert.That(peer.Available, Is.Zero,
                    "Out-of-range TX buffer must not be transmitted");
                Thread.Sleep(10);
            }
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }

    [Test]
    public void DoubleKickTx_WithoutDoneClearedByGuest_DoesNotRetransmit() {
        // Guest sets Own, kicks. The VNIC marks the descriptor in-use, sends,
        // and (by the test's end) sets Done. A second KickTx between the
        // in-flight period must not enqueue a duplicate.
        CatVm vm = NewVm();
        (VirtualNetworkCard vnic, UdpClient peer) = CreatePair(vm, out _, out _);
        try {
            uint frameAddr = 0x200;
            uint descAddr = 0x100;
            byte[] frame = MakeFrame(BroadcastMac(), [1, 2, 3, 4, 5, 6], "single-shot");
            Array.Copy(frame, 0, vm.Memory, frameAddr, frame.Length);
            WriteU32(vm.Memory, descAddr, frameAddr);
            WriteU32(vm.Memory, descAddr + 4, (uint)frame.Length);
            vm.Memory[descAddr + 8] = DescOwn;

            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.SetTxRing);
            vnic.Output(vm, descAddr);
            vnic.Output(vm, 1);

            // Two kicks back-to-back.
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.KickTx);
            vnic.Output(vm, (uint)VirtualNetworkCard.Mode.KickTx);

            // Wait for the first packet.
            IPEndPoint ep = new(IPAddress.Any, 0);
            WaitFor(() => peer.Available > 0, "first TX packet");
            _ = peer.Receive(ref ep);

            // After Done is set, the descriptor still has Done (not Own), so a
            // further kick won't pick it up. Verify no second packet arrives.
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 100) {
                Assert.That(peer.Available, Is.Zero,
                    "Second KickTx must not retransmit the same descriptor");
                Thread.Sleep(10);
            }
        } finally {
            vnic.Down();
            peer.Dispose();
        }
    }
}
