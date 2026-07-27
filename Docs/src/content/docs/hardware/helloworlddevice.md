---
title: Hello World Device
slug: hardware/helloworlddevice
---

> Device Type: `0xC0D370A1`  
> CLI Name: `HelloWorld`

The Hello World device is a simple serial device used for testing and examples.

## Behavior

- `out <port>, <value>` prints `Hello World: <value>` to the host console.
- `in <ret>, <port>` prints `Hello World, sending input: 5` and returns `5`.

## Notes

- Command-line name: `HelloWorld`
- On construction, it also registers itself on port `122` in addition to the port it was initially bound to.
