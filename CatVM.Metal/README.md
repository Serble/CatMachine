# CatVM Metal
This is the Cat machine as a physical machine. It is not a launcher: there are no plugins to load, no
device list to pass, and no host desktop to go back to. It takes the hardware the host has, presents
it to the guest as Cat serial devices, and boots a firmware ROM. Combined with
[CatVM.Bootable](../CatVM.Bootable), which puts it on a bootable Alpine image, the result is a
computer that runs Cat programs and nothing else.

Every device in here is a translation layer. Nothing new is invented: each one implements the same
serial protocol as its counterpart in [Hardware](../Hardware), so software written against the
reference launcher runs unchanged. The display is the real
[RaylibPPU](../Hardware/RaylibPpuDevice), the disks are the real [Disk](../Hardware/DiskDevice)
device with a physical block device underneath it instead of an image file, and a guest shutting
itself down turns the actual machine off.

## Hardware map
The reference launcher assigns ports in the order devices appear on the command line. A physical
machine cannot do that, so Metal has a fixed layout that guests may rely on (see
[Ports.cs](Ports.cs)):

| Port  | Device           | Type         | Comes from |
|-------|------------------|--------------|------------|
| 0     | Hardware manager | `0x296C4EF5` | Required to be port 0 by the serial protocol |
| 1     | Display          | `0xFF64BEF9` | RaylibPPU, fullscreen on the machine's monitor |
| 2     | Keyboard         | `0x2EB3AD76` | RaylibPPU, the machine's keyboard |
| 3     | Mouse            | `0x25A3E57D` | RaylibPPU, the machine's mouse |
| 4     | Timer            | `0xB1F91A0C` | Host clock |
| 16... | Disks            | `0x96818B9A` | Block devices in `/dev`, in discovery order |

Guests do not have to hard code any of this. The hardware manager on port 0 lists everything that is
attached, which is how the firmware finds the disk to boot from. `--list-devices` prints the same map
from the host side without starting the CPU.

## Disks
Disks are found by walking `/sys/block`, so no libc interop is needed and the behaviour is the same
under Native AOT on musl. A device is attached only if it is a whole disk with media in it that the
host is not using: anything mounted or used as swap is skipped, along with its parent disk. **The
disk the machine booted from is therefore never handed to the guest**, without Metal having to know
anything about how it booted. Loop, ram, device-mapper, mdraid and optical devices are ignored too.

Disks are sorted by kernel name so that a given disk keeps the same port across boots. For something
stronger, name it explicitly:

```sh
vm --no-auto-disks --disk /dev/disk/by-id/ata-SomeDisk_1234
```

A path given with `--disk` is taken as the operator's decision and skips every check above, including
the one that keeps the guest away from the host's own disk.

Underneath, [BlockDeviceStream](Hardware/BlockDeviceStream.cs) bounds the medium. The Cat disk
protocol has no way to report a failed transfer, so a guest asking for a block past the end of the
disk must not be able to bring the machine down with a host IO error. Instead it behaves like a
drive: reads past the end return zeroes, writes past the end are dropped. Disks are opened `O_SYNC`
by default, because a physical machine can lose power at any moment and the guest is told a write
finished as soon as it is queued; `--no-sync-writes` trades that for speed. When the CPU stops, the
disks are given a moment to drain before they are closed.

## CPU speed
The CPU runs flat out by default (`Fast`). To limit the speed to simulate a specific clock speed
pass `--ops N` where N is operations per second (or cycles per second).

## Power
`ShutdownSystem` from the hardware manager (or anything else that raises the VM's shutdown event)
powers the physical machine off, via the host's own `poweroff` so that filesystems are unmounted
first. There is nothing behind the machine to return to, so treating a shutdown request as "stop the
program" would just look like a hang. `--no-power-control` disables this while developing.

`ResetSystem` resets the VM, which reloads the firmware ROM and starts over. That is what a reset
button does, and it is much faster than rebooting the host. `HaltSystem` stops the CPU and leaves the
last frame on the screen.

## Firmware
The machine always starts from a firmware ROM, and refuses to start without one. **The firmware is
yours**: Metal has none of its own and no built-in idea of how booting should work. It loads the ROM
at address 0 and starts the CPU there, which is exactly what `catlaunch run --rom` does, so any Cat
program can be a firmware. What that program then does is up to it:

- an OS that asks the hardware manager what disks exist, goes looking for one it recognises, and
  loads itself off it
- a single program with no disk involved at all, like a games console with the cartridge soldered in
- a loader that chain-loads something else, the way a BIOS does

Unless `--firmware` is given, the ROM is looked for in this order:

1. `$CATVM_FIRMWARE`
2. `/etc/catvm/firmware.rom`
3. `/opt/catvm/firmware.rom`
4. `firmware.rom` next to the binary

On the bootable image the ROM you built with is at `/opt/catvm/firmware.rom`, and `/etc/catvm` is
searched first, so a new firmware can be dropped in there without rebuilding the image.
[CatVM.Bootable/examples](../CatVM.Bootable/examples) has two working firmwares to start from.

## Running it
On the bootable image this is all done for you. To try it on a normal desktop, publish it and point
it at a firmware ROM:

```sh
dotnet publish CatVM.Metal -c Release -r linux-x64 -o /tmp/metal

/tmp/metal/CatVM.Metal \
    --firmware ExampleProjects/PlatformerCatnip/bin/main.bin \
    --no-auto-disks --no-fullscreen --no-power-control
```

`--no-auto-disks` keeps it away from your real disks, `--no-fullscreen` keeps it in a window, and
`--no-power-control` stops a guest shutdown from turning your desktop off. `--help` lists everything
else.

## Staying AOT compatible
This project is published as a Native AOT binary so that the image does not need a .NET runtime in
it, and so that it starts in the time a machine is expected to take to come up. That rules out the
reflection-based device discovery the launcher uses: devices are constructed directly in
[Program.cs](Program.cs), and the command line is parsed by hand in
[MetalOptions.cs](MetalOptions.cs). Anything added here has to keep that property - publish it and
check that no trim or AOT warnings appear.

Only the raylib native library is needed at runtime, and only when the guest actually turns the
display on. Note that the `Raylib-cs` package ships a glibc build of it, which is no use on Alpine;
the bootable image builds raylib against musl and puts it next to the binary.

## Not here yet
- No network card, audio or real-time clock, so `VirtualNetworkCard` and friends are not attached.
- One display only, on the primary monitor.
