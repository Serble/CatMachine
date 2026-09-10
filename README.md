# Cat Machine
Cat Machine is a project where we have designed a custom CPU architecture called "Cat CPU" and 
implemented it as a virtual machine as full toolset.

You can read more about this project or try it out on the [Wiki](https://catvm.dev).
Try it out in the [playground](https://catvm.dev/playground).

## Design
- **32bit** CPU.
- Simple [instruction set](https://catvm.dev/vm/instructions/) designed to be easy to remember and understand.
- Inspired by old game consoles.
- Designed for making simple games.
- Powerfull, contains simple [virtual mode](https://catvm.dev/vm/virtual-mode/) and [privledge levels](https://catvm.dev/vm/virtual-mode/).

## Toolset

### Installing CLI tools (NuGet global tools)

The Catnip compiler, Cat assembler, and launcher are currently distributed as .NET global tools:

```sh
dotnet tool install --global CatMachine.Catnip.Compiler
dotnet tool install --global CatMachine.CatAssembler
dotnet tool install --global CatMachine.Launcher
```

After install, use:

- `nipcompile` for Catnip compilation
- `catasm` for Cat assembly
- `catlaunch` to run/debug ROMs

### Cat VM
The Cat VM is where you'll be running your applications.
It simulates the architecture and provides debugging tools.

Try running a test application in our [ASM playground](https://catvm.dev/playground/).

It lives in the `CatVM` folder.

### Cat Assembler ([ASM Reference](https://catvm.dev/assembly/catasm-reference/))
The Cat Assembler is an assembler for a custom flavour of assembly language
designed specifically for this project. It assembles into a binary file
which represents the ROM which is loaded into memory on startup.

Try it out in our [ASM playground](https://catvm.dev/playground/).

It lives in the `CatAssembler` folder.

### Catnip ([Reference](https://catvm.dev/catnip/catnip-reference/))
Catnip is a high-level programming language designed specifically for
programming the Cat VM. It compiles into Cat Assembly.

It lives in the `Catnip` folder.

### Catnip Language Server
**Low priority project**

A language server for Catnip is available in `Catnip.LanguageServer`.
It uses stdio and provides diagnostics, hover, go-to-definition,
document symbols, completion, and semantic tokens.

Run it with:

```
dotnet run --project Catnip.LanguageServer/Catnip.LanguageServer.csproj
```

Neovim (`nvim-lspconfig`) example:

```lua
require('lspconfig').catnip_ls.setup {
  cmd = { "dotnet", "run", "--project", "/path/to/CatMachine/Catnip.LanguageServer/Catnip.LanguageServer.csproj" },
  filetypes = { "catnip" },
}
```

VS Code (`settings.json`) example:

```json
{
  "catnip.languageServer.command": [
    "dotnet",
    "run",
    "--project",
    "/path/to/CatMachine/Catnip.LanguageServer/Catnip.LanguageServer.csproj"
  ]
}
```

### Debug Adapter (VS Code, Neovim, CatIDE)

`catlaunch dap` runs a [Debug Adapter Protocol](https://microsoft.github.io/debug-adapter-protocol/)
server over stdio for both Cat Assembly and Catnip programs: breakpoints on source lines, labels and
addresses; stepping by line or by instruction; the call stack; registers and flags; memory and
disassembly views; and watch/hover expressions (`r0`, `player_x`, `[player_x]:2`, `sp + 8`).
When a ROM was compiled from Catnip its `.debug` file maps every instruction back to the `.nip`
line, so Catnip is debugged at source level with the generated assembly available as a second view
(`"sourceView": "assembly"` switches to it).

All settings come from the `launch` request and mirror `catlaunch run`:

| Attribute | Meaning |
|---|---|
| `program` | ROM path (required); symbols are read from `<program>.debug` |
| `debugTable` | `.debug` file when it is not beside the ROM |
| `cwd` | working directory; hardware devices are discovered from `<cwd>/hardware` |
| `devices` | `["RaylibPpu", "DiskDevice path:disk.img"]`, the same form as `-d` |
| `memory`, `ops`, `fast`, `testInts`, `dumpErrors`, `disableHardwareManager` | the matching launcher flags |
| `stopOnEntry` | stop at the first instruction (default true) |
| `sourceView` | `auto` or `assembly` |
| `extraArgs` | further launcher arguments, verbatim |

**VS Code**: install the extension in `EditorConfigs/vscode` (see its README), then add a
`"type": "catvm"` launch configuration.

**Neovim** (`nvim-dap`):

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

**CatIDE** and other editors spawn `catlaunch dap` and speak DAP over stdio; a project's run
configuration maps onto the launch attributes one to one.

Set `CATVM_DAP_STDERR_VERBOSE=1` to log every message to stderr.

### CatLLVM
**Low priority project**

- Generated ASM will be inefficient

CatLLVM is an LLVM-IR backend for the Cat VM. It accepts LLVM IR text
(`.ll`) and emits Cat Assembly, letting you target CatVM from any LLVM
frontend (clang, rustc, etc.):

```
clang -S -emit-llvm -O0 -m32 -ffreestanding -nostdlib \
      -target i386-unknown-none source.c -o source.ll
dotnet run --project CatLLVM    -- source.ll -o source.cat
catasm source.cat -o source.bin
catlaunch run --rom source.bin
```

It lives in the `CatLLVM` folder. See [CatLLVM/README.md](https://github.com/Serble/CatMachine/tree/main/CatLLVM#readme)
for the supported IR subset and calling convention; see `ExampleProjects/LlvmTest/` for
worked examples.
