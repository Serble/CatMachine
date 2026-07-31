# CatVM Bootable
This builds a minimal Alpine Linux image that exists only to run
[CatVM.Metal](../CatVM.Metal). Write it to a disk, boot a real machine from it, and the machine comes
up as a Cat computer running your firmware. There is no login prompt, no desktop and no window,
Linux is in there, but the only thing you ever see is the guest's display.

The same image runs under QEMU, which is the easy way to try it.

## The firmware is yours
The image is built around one ROM, and the machine always starts from it. Metal has no firmware of
its own: it loads your ROM at address 0 and starts the CPU there. `FIRMWARE` is the only setting
`build.sh` insists on.

```sh
FIRMWARE=../../Cat/CatOS/bin/catos.bin ./build.sh              # an OS you built elsewhere
FIRMWARE=../ExampleProjects/PlatformerCatnip/main.nip ./build.sh   # one game, burnt in
FIRMWARE=examples/console.cat ./build.sh                       # smallest working example
```

A `.cat` or `.nip` source is assembled or compiled for you, in the same container the rest of the
build uses.

| | |
|---|---|
| [examples/console.cat](examples/console.cat) | The program *is* the machine. Finds its ports, fills the screen, recolours on keypress, powers the machine off on Q. Never touches a disk. |
| [examples/diskloader.cat](examples/diskloader.cat) | A BIOS-shaped loader: finds the first disk, reads a boot record from block 0, loads the payload and jumps into it. See [examples/README.md](examples/README.md). |

`/etc/catvm` is searched before `/opt/catvm`, so a new ROM can be dropped in at
`/etc/catvm/firmware.rom` on a built image without rebuilding it.

## What happens when it boots

```
firmware/UEFI  ->  extlinux or GRUB  ->  Linux (quiet)  ->  OpenRC
                                                              |
                                                    catvm service on tty1
                                                              |
                                              X with no window manager
                                                              |
                                        /opt/catvm/vm  ->  your firmware ROM
```

X is used rather than talking to DRM directly because the PPU's tiled display mode needs desktop
OpenGL 3.3, and raylib's DRM backend only offers GLES. There is no window manager, so the display
cannot be moved, resized or closed, and nothing can appear in front of it. Mesa falls back to
software rendering when there is no GPU driver, so the image boots on hardware it has never seen.

Note that the machine never hands the guest the disk it booted from, so a firmware that wants to boot
something off a disk needs a second one, like a USB drive.

## Requirements
On the build host:

- `podman` (or `docker`, via `CONTAINER=docker`) - the .NET AOT binary, raylib and (if it needs it)
  the firmware are all built in containers, so no musl toolchain or .NET SDK is needed on the host
- `sudo` - only the last step needs it: laying down a partitioned, bootable filesystem image
- `qemu-img`, `qemu-nbd`, `rsync`, `sfdisk` - used by `alpine-make-vm-image`
- `dosfstools` as well, for UEFI images

The `nbd` kernel module has to be loadable (`sudo modprobe nbd`); `alpine-make-vm-image` uses it to
mount the image while it fills it in.

## Building

```sh
FIRMWARE=examples/console.cat ./build.sh
```

That does four things: publishes CatVM.Metal as a Native AOT binary for `linux-musl-x64`, builds the
firmware if you gave it a source, builds raylib against musl for X11 and OpenGL 3.3, then hands
everything to `alpine-make-vm-image`. Only the last step asks for root.

Useful options, all environment variables:

| Variable | Default | What it does |
|----------|---------|--------------|
| `FIRMWARE` | *required* | ROM to boot, or a `.cat`/`.nip` source to build |
| `FIRMWARE_ROOT` | source's directory | What the firmware build can see, if the source pulls in files from above itself |
| `OPS` | | Cycles per second, e.g. `50M`. Setting it paces the CPU to that speed; unset means flat out |
| `MEMORY` | | RAM for the machine, e.g. `64M` (the machine's own default is 16M) |
| `BOOT_MODE` | `BIOS` | `BIOS` (extlinux) or `UEFI` (GRUB on the ESP) |
| `IMAGE` | `alpine.raw` | Output image, or a block device to write straight to |
| `IMAGE_SIZE` | `2G` | Size of the image file |
| `ALPINE_BRANCH` | `v3.21` | Alpine branch for the image and the raylib build |
| `KERNEL_FLAVOR` | `lts` | `lts` has the drivers real hardware needs; `virt` is smaller |
| `EXTRA_PACKAGES` | | More apk packages, e.g. GPU firmware |
| `RAYLIB_VERSION` | `5.5` | raylib tag to build; must match what `Raylib-cs` binds to |
| `SKIP_RAYLIB` | `auto` | `auto` reuses `build/lib/libraylib.so` if it is already there |
| `SKIP_IMAGE` | `no` | `yes` builds and stages everything but does not touch root |
| `ALLOW_BLOCK_DEVICE` | `no` | Required if `IMAGE` points at a real device |

`SKIP_IMAGE=yes ./build.sh` is the quick loop while working on Metal itself: it leaves a complete
image tree in `build/skel` that can be poked at directly.

## Trying it in QEMU

```sh
./qemu-run.sh
```

`qemu-run.sh` gives the guest a virtio GPU and a tablet device (so mouse positions line up), and keeps
the Linux console on stdio for when something goes wrong. `BOOT_MODE=UEFI ./qemu-run.sh` boots it
through OVMF instead, and has to match how the image was built.

If the firmware expects a disk to boot from, hand it one:

```sh
BOOT_DISK=catdisk.img ./qemu-run.sh
```

It becomes the machine's first disk (port 16), since the image's own disk is withheld.

## Putting it on real hardware
Write the image to the target disk:

```sh
sudo dd if=alpine.raw of=/dev/sdX bs=4M status=progress conv=fsync
```

or have `build.sh` write to it directly, which skips making a 2 GB file first:

```sh
IMAGE=/dev/sdX ALLOW_BLOCK_DEVICE=yes ./build.sh
```

Two things to know before booting a real machine from it:

- **Boot mode.** The default image is BIOS/legacy (extlinux written to the MBR), which works in QEMU
  and on anything with CSM enabled. Build with `BOOT_MODE=UEFI` for modern firmware; GRUB is then
  installed as `EFI/BOOT/BOOTX64.EFI`, the removable-media path, so no NVRAM entry is needed. UEFI
  implementations vary far more than QEMU does, so treat that path as the one to verify first on your
  own hardware.
- **GPU firmware.** No firmware blobs are included, because the full `linux-firmware` package is
  larger than the image. Modern AMD and some Intel GPUs will not initialise without them, and mesa
  will quietly fall back to software rendering (or X will fail to start). Add what your GPU needs:

  ```sh
  EXTRA_PACKAGES='linux-firmware-amdgpu' FIRMWARE=... ./build.sh   # AMD
  EXTRA_PACKAGES='linux-firmware-i915' FIRMWARE=... ./build.sh     # Intel
  ```

  `IMAGE_SIZE` may need raising to fit them.

## Inside the image

| Path | What it is |
|------|------------|
| `/opt/catvm/vm` | The Metal binary (Native AOT, no runtime needed) |
| `/opt/catvm/firmware.rom` | The firmware ROM the image was built with. `/etc/catvm/firmware.rom` overrides it |
| `/opt/catvm/libraylib.so` | raylib built against musl; found next to the binary |
| `/opt/catvm/start-catvm.sh` | Starts X on its own VT and execs the machine inside it |
| `/opt/catvm/xsession` | Turns screen blanking off, then becomes the machine |
| `/etc/conf.d/catvm` | Configuration: everything here goes to `vm` as arguments |
| `/etc/init.d/catvm` | OpenRC service, supervised so the machine comes back if it dies |
| `/var/log/catvm/catvm.log` | Everything the machine, the guest and X have printed |

The static files come from [overlay](overlay); the build drops the generated ones in beside them.
[configure.sh](configure.sh) does the rest inside the image: the service, switching from mdev to udev
(X's libinput driver needs libudev to enumerate devices), freeing tty1 for the display, and quieting
the kernel down.

## Configuring the machine
`OPS` and `MEMORY` at build time write into `/etc/conf.d/catvm`, which can also be edited on the image
directly and the service restarted. `command_args` is passed straight through to the binary, so
`/opt/catvm/vm --help` is the reference:

```sh
command_args="--memory 64M --ops 50M --no-auto-disks --disk /dev/disk/by-id/ata-SomeDisk_1234"
```

The CPU runs flat out by default. While it is uncapped the VM ignores the cycle rate entirely - uptime
and hardware timers run on real time - so `--ops`/`OPS` only does something once the CPU is paced.
Set it when you want the machine to behave like a Cat machine of a particular speed, and accept that
each frame's work then has to fit in that many cycles.

## When something goes wrong
Logins on tty2 to tty6 are left alone, so `Ctrl+Alt+F2` gets you a root shell even while the machine
is running. From there:

```sh
rc-service catvm stop          # stop the machine, get the console back
tail -f /var/log/catvm/catvm.log
cat /var/log/Xorg.0.log        # if the screen never came up
/opt/catvm/vm --list-devices   # what hardware the machine can see
```

- **Black screen, log says nothing after "starting the CPU".** The guest has not turned the display
  on yet; the window is only created once it picks a display mode. With no bootable disk the example 
  firmware turns the screen red and halts.
- **X fails to start.** Usually a GPU with no firmware (see above) or no KMS driver. `xf86-video-vesa`
  and `xf86-video-fbdev` are installed as fallbacks.
- **The guest has no disks.** `--list-devices` shows what was skipped and why. Anything the host has
  mounted is deliberately withheld.
- **Everything is slow.** Without a GPU driver mesa renders in software. The Cat CPU speed is separate
  and set with `--ops`.
