using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using CatData;
using CatVM;
using CatVM.Serial;

namespace VirtualNetworkCardDevice;

public class VirtualNetworkCard : CommandBasedSerialDevice<VirtualNetworkCard.Mode> {
    public override uint Type => 0x29FEF534;

    /// <summary>
    /// <p>4 bytes - For the buffer address</p>
    /// <p>4 bytes - For the length of the buffer</p>
    /// <p>1 byte  - For <see cref="DescFlags"/></p>
    /// </summary>
    private const uint DescriptorSize = 4 + 4 + 1;

    /// <summary>
    /// <p>6 bytes - For the destination mac</p>
    /// <p>6 bytes - For the source mac</p>
    /// <p>2 bytes - For the packet type</p>
    /// </summary>
    private const uint MinFrameLength = 6 + 6 + 2;

    private uint _txRing;
    private uint _txRingLength;
    
    private uint _rxRing;
    private uint _rxRingLength;
    private uint _rxRingTail;

    /// <summary>
    /// Current iteration of the TX config. If this changes
    /// then any in-use transmit descriptors should be considered invalid
    /// and ignored when they come back as done. This is to handle the case where
    /// the guest reconfigures the transmit ring while we still have pending transmits
    /// from the old configuration.
    /// </summary>
    private int _txConfigGeneration;

    /// <summary>
    /// Current iteration of the RX config. Used to abort an in-flight
    /// <see cref="SendVmBuff"/> if the guest reconfigures (or resets) the RX ring
    /// after the listener thread captured the ring base/length.
    /// </summary>
    private int _rxConfigGeneration;
    
    /// <summary>
    /// Value is always true, use .Contains check to check if in use.
    /// </summary>
    private readonly ConcurrentDictionary<uint, bool> _inUseTxDescriptors = [];
    
    private StatusFlags _statusFlags = StatusFlags.LinkUp;
    private readonly byte[] _macAddress = new byte[6];
    private readonly CatVm _vm;

    private readonly CancellationTokenSource _listeningToken = new();
    private readonly Task _listeningTask;

    private readonly UdpClient _transportClient;

    [CommandLineConstructable("VNic")]
    public VirtualNetworkCard(CatVm vm, string ip, int listenPort = -1)
        : this(vm,
            IPEndPoint.TryParse(ip, out IPEndPoint? endpoint)
                ? endpoint
                : throw new ArgumentException("VNic ip invalid"),
            listenPort) {}
    
    public VirtualNetworkCard(CatVm vm, IPEndPoint peer, int listenPort = -1) {
        _vm = vm;
        _transportClient = listenPort == -1
            ? new UdpClient()
            : new UdpClient(listenPort);
        _transportClient.Connect(peer);

        // setup receiving
        _listeningTask = Task.Run(async () => {
            while (!_listeningToken.IsCancellationRequested) {
                try {
                    UdpReceiveResult result = await _transportClient.ReceiveAsync(_listeningToken.Token);
                    HandleIncomingPacket(result);
                }
                catch (OperationCanceledException) {
                    // Cool, exit
                }
                catch (SocketException) {
                    // Most commonly ConnectionReset from an ICMP "port unreachable"
                    // when the peer isn't listening yet. The connected UdpClient
                    // surfaces that on the next receive; just keep listening so we
                    // recover once the peer comes up.
                }
            }
        });
    }

    private void HandleIncomingPacket(UdpReceiveResult result) {
        byte[] buff = result.Buffer;

        if (buff.Length < MinFrameLength) {
            return;  // drop
        }

        // validate destination
        bool isBroadcast = true;
        for (int i = 0; i < 6; i++) {
            if (buff[i] == 0xFF) continue;
            isBroadcast = false;
            break;
        }
        
        // if it wasn't a broadcast see if it was for us
        if (!isBroadcast) for (int i = 0; i < 6; i++) {
            if (buff[i] != _macAddress[i]) {
                return;  // drop
            }
        }
        
        // forward to VM
        SendVmBuff(buff);
    }

    private void SendVmBuff(byte[] buff) {
        // Snapshot the RX config; if it changes underneath us, bail out instead
        // of writing into stale guest memory.
        int startGen = Volatile.Read(ref _rxConfigGeneration);
        uint rxRing = _rxRing;
        uint rxRingLength = _rxRingLength;

        if (rxRingLength == 0) {
            return;  // not configured
        }

        uint descIndex;
        uint? firstDescIndex = null;
        do {
            descIndex = _rxRingTail;
            if (descIndex == firstDescIndex) {
                // we went all the way around, no room
                // just drop the packet
                RaiseStatus(StatusFlags.PacketDropped);
                _vm.HardwareInterrupt(SpecialInterrupts.NicNotification);
                return;
            }
            // Guard against a wider tail value if the ring shrank under us.
            if (descIndex >= rxRingLength) {
                descIndex = 0;
            }
            firstDescIndex ??= descIndex;
            _rxRingTail = (descIndex + 1) % rxRingLength;
        } while (((DescFlags)_vm.Memory[rxRing + descIndex * DescriptorSize + 8]).HasFlag(DescFlags.Done));

        if (Volatile.Read(ref _rxConfigGeneration) != startGen) {
            return;  // config changed mid-walk, drop
        }

        uint descAddr = rxRing + descIndex * DescriptorSize;
        uint bufferAddr = _vm.ReadWord(descAddr);
        uint bufferLength = _vm.ReadWord(descAddr + 4);
        
        // is it long enough?
        if (bufferLength < buff.Length) {
            return;  // can't hold it, TODO: handle splitting across buffers
        }

        if (bufferAddr + buff.Length > _vm.Memory.Length) {
            return;  // invalid buffer, can't write
        }

        if ((long)descAddr + 8 >= _vm.Memory.Length) {
            return;  // descriptor itself out of range
        }
        
        // write the buffer
        Buffer.BlockCopy(buff, 0, _vm.Memory, (int)bufferAddr, buff.Length);

        // Re-check the generation just before we publish the Done flag so we
        // don't mark a descriptor that no longer belongs to the current ring.
        if (Volatile.Read(ref _rxConfigGeneration) != startGen) {
            return;
        }

        // Release fence: ensure the buffer bytes are visible to other cores
        // before the guest sees Done set on the descriptor.
        Volatile.Write(ref _vm.Memory[descAddr + 8], (byte)(DescFlags.Done | DescFlags.EndOfPacket));

        RaiseStatus(StatusFlags.ReceiveDone);
        _vm.HardwareInterrupt(SpecialInterrupts.NicNotification);
    }

    /// <summary>
    /// Atomically OR a status flag in. Safe to call from any thread.
    /// </summary>
    private void RaiseStatus(StatusFlags flag) {
        StatusFlags current, next;
        do {
            current = _statusFlags;
            next = current | flag;
        } while (Interlocked.CompareExchange(ref _statusFlags, next, current) != current);
    }

    private void ScanTransmitBuff() {
        for (uint i = 0; i < _txRingLength; i++) {
            uint descAddr = _txRing + DescriptorSize * i;
            DescFlags flags = (DescFlags)_vm.Memory[descAddr + 8];

            if (!flags.HasFlag(DescFlags.Own)) {
                continue;  // not owned by device, skip
            }

            if (_inUseTxDescriptors.ContainsKey(descAddr)) {
                continue;  // already in use, skip
            }
            
            // they're giving it to us, let's send it
            uint buffAddr = _vm.ReadWord(descAddr);
            uint buffLength = _vm.ReadWord(descAddr + 4);

            if (_vm.Memory.Length < buffAddr + buffLength) {
                continue;  // invalid buffer, skip
            }
            
            // Copy the buffer up front so the guest is free to mutate the TX
            // payload as soon as the synchronous KickTx returns. The descriptor
            // contract still says the buffer is device-owned until Done is
            // visible, but capturing a snapshot here avoids torn frames if the
            // guest violates that contract.
            byte[] snapshot = new byte[buffLength];
            Buffer.BlockCopy(_vm.Memory, (int)buffAddr, snapshot, 0, (int)buffLength);

            _inUseTxDescriptors[descAddr] = true;

            int currentConfigGen = Volatile.Read(ref _txConfigGeneration);
            Task.Run(() => {
                bool sendOk = true;
                try {
                    _transportClient.Send(snapshot);
                }
                catch (Exception) {
                    sendOk = false;
                }

                if (Volatile.Read(ref _txConfigGeneration) != currentConfigGen) {
                    // config changed while we were sending, just drop the result
                    return;
                }

                if ((long)descAddr + 8 < _vm.Memory.Length) {
                    // mark it as done (release fence to publish the flag write)
                    byte currentFlags = _vm.Memory[descAddr + 8];
                    currentFlags &= (byte)~DescFlags.Own;  // clear owned
                    currentFlags |= (byte)DescFlags.Done;  // set done
                    Volatile.Write(ref _vm.Memory[descAddr + 8], currentFlags);
                }

                RaiseStatus(sendOk ? StatusFlags.TransmitDone
                                   : StatusFlags.TransmitDone | StatusFlags.TransmitError);
                _vm.HardwareInterrupt(SpecialInterrupts.NicNotification);

                _inUseTxDescriptors.TryRemove(descAddr, out _);
            });
        }
    }

    public void Down() {
        _statusFlags &= ~StatusFlags.LinkUp;
        _listeningToken.Cancel();
        _listeningTask.Wait();
        _transportClient.Close();
    }

    protected override int GetArgCount(Mode mode) => mode switch {
        Mode.Reset => 0,
        Mode.SetMac => 2,
        Mode.SetTxRing => 2,
        Mode.SetRxRing => 2,
        Mode.KickTx => 0,
        Mode.GetStatus => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    protected override void RunMode(CatVm vm, Mode mode, List<uint> args) {
        switch (mode) {
            case Mode.Reset:
                Interlocked.Increment(ref _txConfigGeneration);
                Interlocked.Increment(ref _rxConfigGeneration);
                _txRing = 0;
                _txRingLength = 0;
                _rxRing = 0;
                _rxRingLength = 0;
                _rxRingTail = 0;
                _statusFlags = 0;
                for (int i = 0; i < 6; i++) _macAddress[i] = 0;
                _inUseTxDescriptors.Clear();
                break;
            
            case Mode.SetMac:
                for (int i = 0; i < 6; i++) {
                    _macAddress[i] = (byte)((args[i / 4] >> (i % 4 * 8)) & 0xFF);
                }
                break;
            
            case Mode.SetTxRing:
                Interlocked.Increment(ref _txConfigGeneration);
                _txRing = args[0];
                _txRingLength = args[1];
                break;
            
            case Mode.SetRxRing:
                Interlocked.Increment(ref _rxConfigGeneration);
                _rxRing = args[0];
                _rxRingLength = args[1];
                _rxRingTail = 0;
                break;
            
            case Mode.KickTx:
                ScanTransmitBuff();
                break;
            
            case Mode.GetStatus:
                StatusFlags statusFlags = Interlocked.Exchange(ref _statusFlags, StatusFlags.LinkUp);
                InputQueue.Enqueue((uint)statusFlags);
                break;
            
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }
    
    public enum Mode {
        /// <summary>
        /// Reset the device.
        /// </summary>
        Reset = 1,
        
        /// <summary>
        /// Set the mac address used for validation.
        /// </summary>
        SetMac = 2,
        
        /// <summary>
        /// Set the location and size of the transmission ring.
        /// </summary>
        SetTxRing = 3,
        
        /// <summary>
        /// Set the location and size of the receiving ring.
        /// </summary>
        SetRxRing = 4,
        
        /// <summary>
        /// Tell the device that there are things to transmit.
        /// </summary>
        KickTx = 5,
        
        /// <summary>
        /// Get the device status.
        /// </summary>
        GetStatus = 6
    }

    [Flags]
    enum DescFlags : byte {
        /// <summary>
        /// This means that the device owns it.
        /// </summary>
        Own = 0x01,
        
        /// <summary>
        /// This means that this descriptor is the last entry.
        /// If a packet is split across multiple descriptors then use this on
        /// the last one.
        /// </summary>
        EndOfPacket = 0x02,
        
        /// <summary>
        /// Set when the device finishes work on the descriptor.
        /// </summary>
        Done = 0x04
    }
    
    [Flags]
    enum StatusFlags : uint {
        /// <summary>
        /// One or more packets have been received.
        /// </summary>
        ReceiveDone = 0x01,
        
        /// <summary>
        /// One or more packets have been transmitted.
        /// </summary>
        TransmitDone = 0x02,
        
        /// <summary>
        /// There was no room to receive a packet and it was dropped.
        /// </summary>
        PacketDropped = 0x04,
        
        /// <summary>
        /// The link is connected. If not set then transition is not possible.
        /// </summary>
        LinkUp = 0x08,

        /// <summary>
        /// One or more transmits completed with an error.
        /// </summary>
        TransmitError = 0x10
    }
}
