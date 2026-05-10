#!/usr/bin/env python3
"""Build a CatVM disk image from one or more raw application binaries.

Disk layout
-----------
Block 0 (512 bytes) is the partition table:
    offset  size  field
    0x000      8  magic number (51)
    0x008      4  version
    0x00C      4  partition count
    0x010     16  UUID
    0x020    12*  partition entries (type:u32, startBlock:u32, blockCount:u32)
    0x1FC      4  checksum (left as 0 for now)

Partition type 0 is a raw application: the block range simply contains the
application bytes, zero-padded to a whole number of 512-byte blocks.

Usage
-----
    python make_disk.py <app.bin> [<app2.bin> ...] -o disk.img
"""

from __future__ import annotations

import argparse
import struct
import sys
import uuid
from pathlib import Path

BLOCK_SIZE = 512
MAGIC = 51
VERSION = 1

HEADER_FMT = "<QII16s"          # magic, version, partition count, uuid
ENTRY_FMT = "<III"              # type, start block, block count
CHECKSUM_OFFSET = 0x1FC

PARTITION_TYPE_RAW_APP = 0


def _blocks_needed(byte_count: int) -> int:
    return (byte_count + BLOCK_SIZE - 1) // BLOCK_SIZE


def build_disk(app_paths: list[Path], disk_uuid: uuid.UUID | None = None) -> bytes:
    if disk_uuid is None:
        disk_uuid = uuid.uuid4()

    apps = [p.read_bytes() for p in app_paths]

    entries: list[tuple[int, int, int]] = []
    next_block = 1
    for app in apps:
        block_count = _blocks_needed(len(app))
        entries.append((PARTITION_TYPE_RAW_APP, next_block, block_count))
        next_block += block_count

    header = bytearray(BLOCK_SIZE)
    struct.pack_into(
        HEADER_FMT, header, 0,
        MAGIC, VERSION, len(entries), disk_uuid.bytes,
    )

    entry_offset = struct.calcsize(HEADER_FMT)
    entry_size = struct.calcsize(ENTRY_FMT)
    table_end = entry_offset + entry_size * len(entries)
    if table_end > CHECKSUM_OFFSET:
        raise ValueError(
            f"Too many partitions ({len(entries)}); entry table would overflow "
            f"into the checksum slot at 0x{CHECKSUM_OFFSET:X}."
        )
    for i, entry in enumerate(entries):
        struct.pack_into(ENTRY_FMT, header, entry_offset + i * entry_size, *entry)

    # Checksum slot left zeroed; spec says ignore for now.
    struct.pack_into("<I", header, CHECKSUM_OFFSET, 0)

    out = bytearray(header)
    for app in apps:
        out.extend(app)
        pad = (-len(app)) % BLOCK_SIZE
        if pad:
            out.extend(b"\x00" * pad)

    return bytes(out)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("apps", nargs="+", type=Path,
                        help="Raw CatVM application binaries to embed as partitions.")
    parser.add_argument("-o", "--output", type=Path, default=Path("disk.img"),
                        help="Output disk image path (default: disk.img).")
    parser.add_argument("--uuid", type=uuid.UUID, default=None,
                        help="Optional fixed disk UUID; defaults to a random one.")
    args = parser.parse_args(argv)

    for p in args.apps:
        if not p.is_file():
            parser.error(f"app binary not found: {p}")

    image = build_disk(args.apps, disk_uuid=args.uuid)
    args.output.write_bytes(image)

    print(f"Wrote {len(image)} bytes ({len(image) // BLOCK_SIZE} blocks) to {args.output}")
    for i, p in enumerate(args.apps):
        size = p.stat().st_size
        print(f"  partition {i}: {p.name} ({size} bytes, {_blocks_needed(size)} block(s))")
    return 0


if __name__ == "__main__":
    sys.exit(main())
