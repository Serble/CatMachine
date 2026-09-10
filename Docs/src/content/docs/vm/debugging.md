---
title: Debugging
slug: vm/debugging
---

CatVM ships two debuggers over the same core: an interactive terminal debugger (`catlaunch debug`)
and a [Debug Adapter Protocol](https://microsoft.github.io/debug-adapter-protocol/) server
(`catlaunch dap`) for editors. Both read the `.debug` symbol file the assembler writes beside every
ROM (see [Debug Symbols](/assembly/catasm-reference/#debug-symbols)), so they work for hand-written
Cat Assembly and for Catnip alike: a Catnip build maps each instruction back to the `.nip` line it
came from, and the debuggers show that line while keeping the generated assembly available.

## Terminal debugger

```sh
catlaunch debug --rom bin/main.bin -d RaylibPpu
```

Every prompt shows the registers, the bytes at `ip`, the current source line, watches and the call
stack. Commands:

| Command | Effect |
|---|---|
| `step` / `s` | execute one instruction |
| `step-over` / `sover` | execute one instruction, running a `call` to completion |
| `step-out` / `sout` | run until the current function returns |
| `continue` / `c` | run until a breakpoint, a fault, or a pause |
| `continue-for` / `cf instr|secs <n>` | run for a number of instructions or seconds |
| `break` / `b symbol <label>` | breakpoint on a label |
| `break line <n>` or `break line <file>:<n>` | breakpoint on the first instruction of a source line (the file form is needed when a line number occurs in several files) |
| `break address <addr>` | breakpoint on an address (`0x` hex or decimal) |
| `clear-breaks` / `cb` | remove all breakpoints |
| `watch` / `w <name> <symbol|line|address> <point> [1|2|4]` | show a memory value at every prompt |
| `dump-memory` / `dm [symbol|line|address] [point] [size]` | dump memory to the console and `memory_dump.bin` |
| `set-register` / `sr <reg> <value>` | change a register |

## Editor debugging (`catlaunch dap`)

`catlaunch dap` takes no command-line arguments: everything comes from the editor's launch
configuration, which mirrors `catlaunch run`.

| Attribute | Meaning |
|---|---|
| `program` | ROM path (required); symbols are read from `<program>.debug` |
| `debugTable` | `.debug` file when it is not beside the ROM (`nipcompile -d` lets you put it anywhere) |
| `cwd` | working directory; hardware devices are discovered from `<cwd>/hardware` |
| `devices` | devices to attach in the `-d` form: `"RaylibPpu"`, `"DiskDevice path:disk.img"`, or `{"name": "DiskDevice", "args": {"path": "disk.img"}}` |
| `memory`, `ops`, `fast`, `testInts`, `dumpErrors`, `disableHardwareManager` | the matching launcher flags |
| `stopOnEntry` | stop at the first instruction (default `true`) |
| `sourceView` | `auto` shows Catnip lines when the ROM has them; `assembly` always shows the assembly |
| `extraArgs` | further launcher arguments, verbatim |

What the adapter provides:

- **Breakpoints** on source lines (in `.cat` or `.nip` files), on labels (function breakpoints),
  and on addresses (instruction breakpoints). A line with no code is reported as unverified.
- **Stepping** by line (`next` / `step in` / `step out`) or by instruction when the editor asks
  for instruction granularity (VS Code: the disassembly view). A Catnip statement that compiled
  to several instructions is one step.
- **Call stack** from `call`/`ret` tracking, with function names from labels and each frame on
  the line of its call.
- **Registers** (`r0`–`r7`, `sp`, `ip`, `fl`), **Flags** (zero, carry, sign, overflow) and
  **CPU** state (mode, interrupt table, kernel stack pointer, memory base/length). Registers and
  flags can be edited.
- **Memory** reading and writing (VS Code's hex editor "View Binary Data" on `sp`, `ip`, a
  label or a watch), and **disassembly** built from the debug table.
- **Expressions** for watches, hovers and the debug console:

  | Expression | Meaning |
  |---|---|
  | `r3`, `sp`, `$ip` | a register |
  | `player_x`, `SCREEN_W` | a label or `#define` constant |
  | `0x1F`, `0b101`, `42` | a number |
  | `[player_x]`, `[sp + 4]:1`, `[r1]:2` | memory at an address, as a word, byte or half-word |
  | `a + b * 2`, `-(x - 1)` | arithmetic |

- **Guest faults** (page fault, invalid instruction, divide by zero) stop the session as an
  exception. Untick the "Guest CPU faults" filter to run into the guest's own handler instead.
- **Guest output** (`int 0x80`, `int 0x90`) appears in the debug console.

Set `CATVM_DAP_STDERR_VERBOSE=1` to log every protocol message to stderr.

### VS Code

Install the extension in `EditorConfigs/vscode` of the repository (its README has the steps),
then use a launch configuration such as:

```json
{
  "type": "catvm",
  "request": "launch",
  "name": "Debug ROM",
  "program": "${workspaceFolder}/bin/main.bin",
  "cwd": "${workspaceFolder}",
  "preLaunchTask": "build",
  "devices": ["RaylibPpu"],
  "fast": true,
  "stopOnEntry": true
}
```

### Neovim

With [nvim-dap](https://github.com/mfussenegger/nvim-dap):

```lua
local dap = require('dap')
dap.adapters.catvm = { type = 'executable', command = 'catlaunch', args = { 'dap' } }
local launch = {
  {
    type = 'catvm', request = 'launch', name = 'Debug ROM',
    program = '${workspaceFolder}/bin/main.bin',
    cwd = '${workspaceFolder}',
    devices = { 'RaylibPpu' },
    fast = true, stopOnEntry = true,
  },
}
dap.configurations.catasm = launch
dap.configurations.catnip = launch
vim.filetype.add({ extension = { cat = 'catasm', nip = 'catnip' } })
```

### Other editors

Anything that speaks DAP can spawn `catlaunch dap` and talk to it over stdio. A project's run
configuration (ROM, devices, memory, fast) maps onto the launch attributes one to one.

## Limitations

- Breakpoints and symbols use physical addresses. Code running in
  [virtual mode](/vm/virtual-mode/) is not mapped back through `mbase`.
- The call stack is tracked from `call` and `ret` only; an interrupt handler that is paused shows
  the interrupted function's frames beneath it, and `step out` refuses when no call has been seen.
- Conditional breakpoints are accepted but their condition is ignored (the breakpoint is reported
  with a message saying so).
