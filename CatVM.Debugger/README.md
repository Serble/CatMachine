# CatVM Debugger
This is an interactive debugger for debugging CatVM applications. It used to exist in the CatVM project but has been
moved because it uses the JSON deserialiser which uses reflection and is therefore inompatible with AOT which
CatVM is designed to be. Also being in a seperate library allows the main project to be lighter.

The best way to use the debugger is with the launcher.

```sh
catlaunch debug rom.img
```

Or if you want to integrate it into another project:
```csharp
string romPath = "/path/to/some/rom.img";
CatVm vm = ...;  // some preconfigured VM instance

CatVmDebugger debugger = new(vm, romPath);
debugger.StartUserDebugging();
```
