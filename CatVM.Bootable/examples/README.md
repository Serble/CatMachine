# Booting off a disk

[`diskloader.cat`](diskloader.cat) is a BIOS-shaped loader: it finds the first disk, reads a boot
record from block 0, loads the payload it describes, and jumps into it. It is one way a firmware can
find something to boot, written down so there is a worked example; a real OS would define its own
layout and its own tooling.

The loader reads block 0 of the first disk, expects a boot record, and loads the payload it describes:

| Offset | Size | Field |
|--------|------|-------|
| 0 | 4 | magic, the ASCII bytes `CATB` |
| 4 | 4 | address to load the payload at |
| 8 | 4 | payload length in 512 byte blocks, read from block 1 onwards |
| 12 | 4 | entry point |

`../tools/mkbootdisk.sh` writes all of that:

```sh
../tools/mkbootdisk.sh payload.rom catdisk.img [origin] [size]
```

`catasm` emits flat ROMs whose labels are absolute addresses counted from 0, so a payload that is
going to run somewhere else has to reserve the space below it and let `mkbootdisk` strip it back off:

```
#const ORG, 0x2400
    res8 ORG
main:
    ; ...
```

`0x2400` is the lowest origin the loader accepts: its own code, its scratch block and the boot info
structure sit underneath it. Note that this only applies to payloads. A firmware is loaded at 0, so it
needs none of this - which is why a program can be a firmware unmodified.

Catnip payloads need the same padding, which is easy because the compiler can emit its assembly:

```sh
cd ExampleProjects/PlatformerCatnip
nipcompile main.nip -o bin/main.bin -s bin/main.asm -d bin/main.asm.debug

{ echo '    res8 0x2400'; cat bin/main.asm; } > bin/main.boot.asm
catasm bin/main.boot.asm -o "$PWD/bin/main.boot.rom"

cd ../../CatVM.Bootable
./tools/mkbootdisk.sh ../ExampleProjects/PlatformerCatnip/bin/main.boot.rom platformer.img 0x2400 32M
FIRMWARE=examples/diskloader.cat ./build.sh
BOOT_DISK=platformer.img ./qemu-run.sh
```

On entry the payload gets `r1` pointing at a boot info block describing the machine - memory size and
the port of every device the loader found. The layout is documented at the top of
[`diskloader.cat`](diskloader.cat). Everything else (the framebuffer, the stack, the interrupt table)
belongs to the payload; the loader leaves the framebuffer at the top of memory with the stack below
it, and a payload is free to move both.
