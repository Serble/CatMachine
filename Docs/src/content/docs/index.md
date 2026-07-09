---
title: Home
---

<img src="favicon.svg" width="128" height="128">

## Welcome to the Cat Machine wiki!

Cat Machine is a project that defines a 32bit CPU architecture, implements it in a VM, and implements an assembler for a custom assembly language. The inspiration behind this project is old consoles, the idea is that you can write a game or application using our ASM and then run it with a limited CPU clock rate to simulate poor hardware, this means that you can focus on optimisation knowing that it will mean something.

### The VM

The VM allows configuring the CPU cycles per second, the available memory, and the available debugging tools. You pass the VM a ROM file, which is then loaded into memory and execution begins at address `0`.

### The Assembly

The assembler supports most modern assembler features like assemble-time constants and expressions, `#include`, macros, reserving and defining raw data, including raw data files, etc. Every assembled ROM is paired with a JSON debug-symbol file so debuggers and other tooling can show source lines and symbolic addresses.
