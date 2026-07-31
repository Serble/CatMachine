---
title: Disk Device
slug: hardware/diskdevice
---

> Device Type: `0x96818B9A`  
> Interrupt on completion: `0x72` (`DiskOperationFinish`) — read and write only  
> CLI Name: `Disk`

The disk device is a command-based serial device that reads and writes fixed-size blocks, and
reports its capacity.

## Block format

- Block size is **512 bytes**.
- Operations are queued and executed serially.

## Commands

| Command | ID | Args |
|---------|----|------|
| [Read](#read) | 1 | 3 |
| [Write](#write) | 2 | 3 |
| [Get Size](#get-size) | 3 | 0 |

Read and write both take:
1. VM memory address
2. start block
3. block count

Get size takes no arguments.

## Read

Copies `block count` blocks from disk into VM memory starting at the provided memory address.

## Write

Copies `block count` blocks from VM memory into disk starting at the provided block number.

## Get Size

The next value the device emits from the serial protocol is the disk's capacity **in blocks**, so
the size in bytes is that value multiplied by 512. A disk whose length is not a whole number of
blocks is reported rounded down, since a partial trailing block cannot be addressed.

Unlike read and write this is answered immediately and does **not** raise `0x72`, so read the
reply straight after issuing the command rather than waiting for the completion interrupt. It
may be issued while a read or write is still in flight; it does not queue behind them.

## Completion

After each read or write finishes, the device raises hardware interrupt `0x72`. Get size is
synchronous and never raises it.

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

; Ask the disk how big it is
OUT DISK_PORT, 3      ; Get Size
IN  r0, DISK_PORT     ; capacity in 512-byte blocks
```
