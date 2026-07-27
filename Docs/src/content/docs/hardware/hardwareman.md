---
title: Hardware Manager
slug: hardware/hardwareman
---

> Device Type: `0x296C4EF5`  
> CLI Name: `HardwareManager`

The hardware manager is the generic serial device that can describe the other serial devices added to the VM
to the running application.

Unlike other devices which are generally assigned to any free port, **by convention the hardware manager
will be bound to port `0` if present**.

### Protocol
The hardware manager is a command based serial device, so you start by sending it a command, and then a set of
arguments, after which it will response with a predefined amount of return values.

| Command                             | ID |
|-------------------------------------|----|
| [List Devices](#list-devices)       | 1  |
| [Halt System](#halt-system)         | 2  |
| [Shutdown System](#shutdown-system) | 3  |
| [Reset System](#reset-system)       | 4  |

### Commands

#### List Devices
Output the device count, and then the following for each device:
- *the port* the device is bound to, and
- *the device's type*,  

in that order.

for example:
```cat
#define HARDWARE_MAN_PORT, 0
#define LIST_DEVICES, 1
#define HALT, 2

out HARDWARE_MAN_PORT, LIST_DEVICES

in r0, HARDWARE_MAN_PORT      ; read the device count

loop:
    in r1, HARDWARE_MAN_PORT  ; read device port
    in r2, HARDWARE_MAN_PORT  ; read device type
    
    int 0x90                  ; print port
    
    mov r1, r2
    int 0x90                  ; print type
    
    sub r0, 1
    cmp r0, 0                 ; check if we've read all the devices
    jug loop

; done!
; we successfully looped through and debug printed the information for every
; available device.
out HARDWARE_MAN_PORT, HALT
```

#### Halt System
Cease execution of the VM permanently, the VM will be frozen, this cannot be undone.

#### Shutdown System
Stop the VM.

#### Reset System
Reset the VM to it's initial CPU state.
