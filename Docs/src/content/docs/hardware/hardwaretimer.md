---
title: Hardware Timer
slug: hardware/hardwaretimer
---

The hardware timer is designed so that programs can asyncronously schedule timers that will emit an interrupt
upon completion. This can be used to avoid repeatedly polling `UPTMS` or for situations where this isn't possible
like interrupting running code after a certain amount of time (which allows for task switching mechanisms).

## Protocol
The hardware timer is a command based device which means you start by sending it a number representing what
you'd like to you, then a number of arguments.

| Command                             | ID |
|-------------------------------------|----|
| [New Timer](#new-timer)             | 1  |

#### New Timer
Send a value for the duration of the timer in milliseconds, and then a timer ID. Once the time elapses the timer will emit an
interrupt with code `0x71`, and the next value it will emit from the serial protocol will be the timer ID provided upon creation.

For example to create a timer for 10ms do:
```cat
OUT 0x03, 0x01     ; New Timer
OUT 0x03, 10       ; 10ms
OUT 0x03, 5        ; Timer ID of 5
; Timer is now set
```
