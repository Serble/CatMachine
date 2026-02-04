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

### CatASM IntelliJ Plugin
An IntelliJ IDEA plugin that provides syntax highlighting and language support
for CatASM files (.asm). Features include syntax highlighting for instructions,
registers, directives, labels, and comments.

It lives in the `CatASM-IntelliJ-Plugin` folder.

See the [plugin README](CatASM-IntelliJ-Plugin/README.md) for installation and usage instructions.
