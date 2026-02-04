# CatASM IntelliJ Plugin

An IntelliJ IDEA plugin that provides language support for CatASM (Cat Assembly), the assembly language for Cat Machine.

## Features

- **Syntax Highlighting**: Color-coded syntax for:
  - Instructions (mov, add, jmp, etc.)
  - Registers (r0-r7, sp, bp, ip, flags)
  - Directives (#const, #include, d8, d16, d32, etc.)
  - Labels (local and global)
  - Numbers (decimal and hexadecimal)
  - Strings
  - Comments
  
- **File Type Recognition**: Automatic recognition of .asm files as CatASM files
- **Custom Icon**: Visual identifier for CatASM files in the project tree

## Building

To build the plugin:

```bash
./gradlew buildPlugin
```

The plugin will be built in `build/distributions/`.

## Installation

1. Build the plugin as described above
2. In IntelliJ IDEA, go to Settings → Plugins
3. Click the gear icon and select "Install Plugin from Disk..."
4. Select the built .zip file from `build/distributions/`
5. Restart IntelliJ IDEA

## Development

To run the plugin in a development instance:

```bash
./gradlew runIde
```

## CatASM Language

CatASM is the assembly language for Cat Machine, a custom 32-bit CPU architecture. The language includes:

- 32-bit instructions with various addressing modes
- 8 general-purpose registers (r0-r7)
- Special registers (sp, bp, ip, flags)
- Support for 8, 16, and 32-bit operations
- Jump and call instructions
- Interrupt system
- I/O operations

For more information about CatASM and Cat Machine, see the [main repository](https://github.com/Serble/CatMachine).

## License

See the main repository's LICENSE file.
