---
title: Disk Device
slug: hardware/diskdevice
---

> Device Type: `0x96818B9A`  
> Interrupt on completion: `0x72` (`DiskOperationFinish`)  
> CLI Name: `Disk`

The disk device is a command-based serial device that reads and writes fixed-size blocks.

## Block format

- Block size is **512 bytes**.
- Operations are queued and executed serially.

## Commands

| Command | ID | Args |
|---------|----|------|
| [Read](#read) | 1 | 3 |
| [Write](#write) | 2 | 3 |

All commands take:
1. VM memory address
2. start block
3. block count

## Read

Copies `block count` blocks from disk into VM memory starting at the provided memory address.

## Write

Copies `block count` blocks from VM memory into disk starting at the provided block number.

## Completion

After each operation finishes, the device raises hardware interrupt `0x72`.

## Example

```cat
; Read 1 block from disk block 4 into memory at 0x200
OUT DISK_PORT, 1      ; Read
OUT DISK_PORT, 0x200  ; memory address
OUT DISK_PORT, 4      ; start block
OUT DISK_PORT, 1      ; block count

; Write 1 block from memory 0x200 to disk block 8
OUT DISK_PORT, 2      ; Write
OUT DISK_PORT, 0x200
OUT DISK_PORT, 8
OUT DISK_PORT, 1
```
