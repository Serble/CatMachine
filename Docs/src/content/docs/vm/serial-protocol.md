---
title: Serial Protocol
slug: vm/serial-protocol
---

There are two types of serial instructions, `in` and `out`.
You specify a port and then get or receive data from that port.
In the VM, virtual 'hardware' can register itself as a serial device, this allows applications to interact with a wide variety of things using one simple protocol.

## Ports
A port number is a `uint` (4 bytes) and therefore has too many different values for applications to scan.
The idea is specific hardware configurations can define the ports each piece of hardware is on, and then
software written for it can assume the ports.

Otherwise for more generic cases there is a builtin serial device called the [Hardware Manager](/hardware/hardwareman), when the
hardware manager is present it is always at port `0`.

Without the hardware manager or another similar device being assumed to exist there is no way to definitively
probe for present devices. In most cases the hardware manager should be kept if you want your hardware configuration
to be compatible with a broad range of applications.
