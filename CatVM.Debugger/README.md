# CatVM Debugger
Debugger core for CatVM plus the interactive terminal front-end. It lives outside `CatVM` so the
VM library stays lean; the whole project is AOT compatible (the `.debug` tables are parsed with
hand-written `JsonNode` code rather than reflection).

- `CatDebugger` - headless core: breakpoints, a shadow call stack, `Step`/`StepOver`/`StepOut`/`RunUntil`,
  fault and shutdown reporting. Front-ends build on this.
- `DebugTableIndex` - indexed queries over a `DebugTable`: address -> symbol, `file:line` -> address,
  label -> address, "which function contains this address".
- `TuiVmDebugger` - the REPL used by `catlaunch debug`.

The DAP server that editors use (`catlaunch dap`) is a separate front-end in `CatVM.DebugAdapter`.

The best way to use the debugger is with the launcher.

```sh
catlaunch debug --rom rom.img
```

Or if you want to integrate it into another project:
```csharp
string romPath = "/path/to/some/rom.img";
CatVm vm = ...;  // some preconfigured VM instance

TuiVmDebugger debugger = new(vm, romPath);
debugger.StartUserDebugging();
```
