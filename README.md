# Cat Machine
Cat Machine is a project where we have designed a custom CPU architecture called "Cat CPU" and 
implemented it as a virtual machine as full toolset.

You can read more about this project on the [GitHub Wiki Page](https://github.com/Serble/CatMachine/wiki).

## Design
- **32bit** CPU.
- Simple architecture designed to be easy to remember and understand.
- Designed to mimic an old game console.
- Designed for making simple games.

## Toolset

### Cat VM
The Cat VM is where you'll be running your applications.
It simulates the architecture and provides debugging tools.

It lives in the `CatVM` folder.

### Cat Assembler
The Cat Assembler is an assembler for a custom flavour of assembly language
designed specifically for this project. It assembles into a binary file
which represents the ROM which is loaded into memory on startup.

It lives in the `CatAssembler` folder.

### Catnip
Catnip is a high-level programming language designed specifically for
programming the Cat VM. It compiles into Cat Assembly.

It lives in the `Catnip` folder.

### Catnip Language Server
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

### CatLLVM
CatLLVM is an LLVM-IR backend for the Cat VM. It accepts LLVM IR text
(`.ll`) and emits Cat Assembly, letting you target CatVM from any LLVM
frontend (clang, rustc, etc.):

```
clang -S -emit-llvm -O0 -m32 -ffreestanding -nostdlib \
      -target i386-unknown-none source.c -o source.ll
dotnet run --project CatLLVM    -- source.ll -o source.cat
dotnet run --project CatAssembler -- source.cat -o source.bin
dotnet run --project CatVM      -- source.bin
```

It lives in the `CatLLVM` folder. See `CatLLVM/README.md` for the supported
IR subset and calling convention; see `ExampleProjects/LlvmTest/` for
worked examples.
