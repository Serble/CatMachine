# CatVM Debugger for VS Code

Debug Cat Assembly (`.cat`) and Catnip (`.nip`) programs running on CatVM from VS Code: gutter
breakpoints, stepping (by source line or by instruction), registers and flags, a memory view, a
disassembly view, and watch/hover expressions. The adapter is `catlaunch dap`; this extension only
starts it and registers the two languages.

## Install

1. Install the launcher: `dotnet tool install --global CatMachine.Launcher` (or point
   `catvm.catlaunchPath` at a source checkout, see below).
2. Install the extension from this folder:

   ```sh
   cd EditorConfigs/vscode
   npx @vscode/vsce package          # produces catvm-debug-0.1.0.vsix
   code --install-extension catvm-debug-0.1.0.vsix
   ```

   While developing, a symlink works too: `ln -s "$(pwd)" ~/.vscode/extensions/catvm-debug`.

## launch.json

```json
{
  "version": "0.2.0",
  "configurations": [
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
  ]
}
```

`program` is the assembled ROM; its symbols are read from `<program>.debug`, which `catasm`
writes automatically and `nipcompile` writes with `-d`. Build first with a `preLaunchTask`, e.g. a
`tasks.json` entry that runs `catasm main.cat -o bin/main.bin` or `nipcompile main.nip -o bin/main.bin -s bin/main.asm -d bin/main.bin.debug`.

All launch attributes:

| Attribute | Meaning |
|---|---|
| `program` | ROM path (required) |
| `debugTable` | `.debug` file when it is not beside the ROM |
| `cwd` | working directory; hardware devices are discovered from `<cwd>/hardware` |
| `devices` | devices to attach, as `catlaunch run -d` takes them: `"RaylibPpu"`, `"DiskDevice path:disk.img"` or `{"name": "DiskDevice", "args": {"path": "disk.img"}}` |
| `memory`, `ops`, `fast`, `testInts`, `dumpErrors`, `disableHardwareManager` | the matching `catlaunch run` flags |
| `stopOnEntry` | stop at the first instruction (default true) |
| `sourceView` | `auto` (Catnip lines when available) or `assembly` |
| `extraArgs` | further `catlaunch run` arguments, verbatim |

## Debugging against a source checkout

Set `catvm.catlaunchPath` to a command that runs the launcher from source:

```json
"catvm.catlaunchPath": "dotnet run --project /path/to/CatMachine/CatLauncher/CatLauncher.csproj --"
```

## Grammars

`syntaxes/` holds copies of `EditorConfigs/*.tmLanguage.json`; the originals are canonical. After
editing them run `npm run sync-grammars` here to refresh the copies.
