---
title: Virtual Network Card (VNIC)
slug: hardware/vnic
---

> Device Type: `0x29FEF534`  
> CLI Name: `Vnic`  
> Event Interrupt: `0x73` (`NicNotification`)

The virtual network card is a command-based serial device that sends and receives Ethernet-like frames over UDP.

## Descriptor layout

Each TX/RX ring entry is 9 bytes:

| Offset | Size | Meaning |
|--------|------|---------|
| 0      | 4    | Guest buffer address |
| 4      | 4    | Buffer length in bytes |
| 8      | 1    | Descriptor flags |

### Descriptor flags

| Flag | Value | Meaning |
|------|-------|---------|
| `Own` | `0x01` | Descriptor is owned by the device |
| `EndOfPacket` | `0x02` | Last descriptor for a packet |
| `Done` | `0x04` | Device finished processing descriptor |

## Status flags (`GetStatus`)

| Flag | Value | Meaning |
|------|-------|---------|
| `ReceiveDone` | `0x01` | One or more frames were received |
| `TransmitDone` | `0x02` | One or more frames were transmitted |
| `PacketDropped` | `0x04` | RX ring had no available descriptor |
| `LinkUp` | `0x08` | Link is up |
| `TransmitError` | `0x10` | One or more TX operations failed |

`GetStatus` returns current flags, then clears all flags except `LinkUp`.

## Commands

| Command | ID | Args |
|---------|----|------|
| [Reset](#reset) | 1 | none |
| [Set MAC](#set-mac) | 2 | 2 |
| [Set TX ring](#set-tx-ring) | 3 | 2 |
| [Set RX ring](#set-rx-ring) | 4 | 2 |
| [Kick TX](#kick-tx) | 5 | none |
| [Get status](#get-status) | 6 | none |

### Reset

Clears MAC address, ring configuration, and internal state.

### Set MAC

Sets the local MAC address used for incoming unicast filtering.

Arguments are packed little-endian:
- `arg0`: bytes 0..3
- `arg1`: bytes 4..5

### Set TX ring

Arguments:
1. ring base address
2. descriptor count

### Set RX ring

Arguments:
1. ring base address
2. descriptor count

### Kick TX

Scans the TX ring and sends descriptors with `Own` set.

### Get status

Enqueues one status value for `in` to read.

## RX filtering behavior

Incoming frames are accepted when:
- frame length is at least 14 bytes
- destination MAC is broadcast (`FF:FF:FF:FF:FF:FF`) or matches the configured MAC

Otherwise the frame is dropped.
