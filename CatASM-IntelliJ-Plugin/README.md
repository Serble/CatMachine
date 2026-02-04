# CatASM IntelliJ Plugin

An IntelliJ IDEA plugin that provides language support for CatASM (Cat Assembly), the assembly language for Cat Machine.

## Features

- **Syntax Highlighting**: Color-coded syntax for:
  - Instructions (mov, add, jmp, call, ret, etc.)
  - Registers (r0-r7, sp, bp, ip, flags)
  - Directives (#const, #include, d8, d16, d32, dfile, dstr, etc.)
  - Labels (local `.label` and global `label:`)
  - Numbers (decimal and hexadecimal with `0x` prefix)
  - Strings (single and double quoted)
  - Comments (`;` line comments)
  
- **File Type Recognition**: Automatic recognition of .asm files as CatASM files
- **Custom Icon**: Visual identifier for CatASM files in the project tree

## Building

### Prerequisites

- Java 17 or later
- Gradle (the project includes a Gradle wrapper)
- Internet connection (to download IntelliJ Platform SDK)

### Build Instructions

To build the plugin from source:

```bash
cd CatASM-IntelliJ-Plugin

# On Linux/macOS:
./gradlew buildPlugin

# On Windows:
gradlew.bat buildPlugin
```

The plugin will be built as a `.zip` file in `build/distributions/`.

### Running in Development Mode

To test the plugin in a sandboxed IntelliJ instance:

```bash
./gradlew runIde
```

This will download the IntelliJ Platform SDK and launch a new IDE instance with your plugin installed.

## Installation

### From Built Plugin

1. Build the plugin as described above
2. In IntelliJ IDEA, go to **Settings** → **Plugins**
3. Click the gear icon (⚙️) and select **Install Plugin from Disk...**
4. Select the built `.zip` file from `build/distributions/`
5. Restart IntelliJ IDEA

### Manual Installation (Development)

If you have the plugin source code:

1. Open IntelliJ IDEA
2. Go to **File** → **Open** and select the `CatASM-IntelliJ-Plugin` directory
3. Wait for Gradle to sync
4. Run the `runIde` Gradle task
5. A new IDE window will open with the plugin loaded

## Usage

Once installed, the plugin will automatically provide syntax highlighting for all `.asm` files. The plugin recognizes:

### Instructions
All CatASM instructions including:
- Data movement: `mov`, `mov8`, `mov16`, `mov32`
- Arithmetic: `add`, `sub`, `imul`, `umul`, `idiv`, `udiv`
- Bitwise: `and`, `or`, `xor`, `not`, `shl`, `shr`
- Stack: `push`, `pop`, `push8`, `push16`, `push32`, `pop8`, `pop16`, `pop32`
- Control flow: `jmp`, `call`, `ret`, `jz`, `je`, `jnz`, `jne`, `jul`, `jule`, `jug`, `juge`, `jil`, `jile`, `jig`, `jige`
- Comparison: `cmp`
- Interrupts: `int`, `di`, `ei`
- I/O: `in`, `out`
- Memory: `cpy`
- Misc: `nop`

### Directives
- Constants: `#const`
- Includes: `#include`
- Data definitions: `d8`, `d16`, `d32`, `dfile`, `dstr`
- Reservations: `res8`, `res16`, `res32`

### Registers
- General purpose: `r0`, `r1`, `r2`, `r3`, `r4`, `r5`, `r6`, `r7`
- Special: `sp` (stack pointer), `bp` (base pointer), `ip` (instruction pointer), `flags`

### Example Code

```asm
; Example CatASM code
#const SCREEN_WIDTH, 512
#const SCREEN_HEIGHT, 512

main:
    mov r0, 0           ; Initialize counter
    mov r1, SCREEN_WIDTH
    
.loop:
    add r0, 1           ; Increment counter
    cmp r0, r1          ; Compare with width
    jule .loop          ; Loop if less or equal
    
    call draw_screen    ; Call function
    ret                 ; Return
    
draw_screen:
    push r0             ; Save register
    ; ... drawing code ...
    pop r0              ; Restore register
    ret
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
- Memory addressing with `@` prefix for pointers

For more information about CatASM and Cat Machine, see the [main repository](https://github.com/Serble/CatMachine).

## Development

### Project Structure

```
CatASM-IntelliJ-Plugin/
├── src/main/
│   ├── kotlin/com/serble/catmachine/catasm/
│   │   ├── CatAsmLanguage.kt           # Language definition
│   │   ├── CatAsmFileType.kt           # File type definition
│   │   ├── CatAsmLexer.kt              # Lexical analyzer
│   │   ├── CatAsmParser.kt             # Parser
│   │   ├── CatAsmParserDefinition.kt   # Parser configuration
│   │   ├── CatAsmSyntaxHighlighter.kt  # Syntax highlighting
│   │   ├── CatAsmTypes.kt              # Token types
│   │   └── ...
│   └── resources/
│       ├── META-INF/plugin.xml         # Plugin metadata
│       └── icons/catasm.svg            # File icon
├── build.gradle.kts                    # Build configuration
└── README.md                           # This file
```

### Adding New Features

To add new features to the plugin:

1. **New Instructions**: Add to the `INSTRUCTIONS` set in `CatAsmLexer.kt`
2. **New Directives**: Add to the `DIRECTIVES` set in `CatAsmLexer.kt`
3. **New Token Types**: Add to `CatAsmTypes.kt`
4. **Color Customization**: Modify `CatAsmSyntaxHighlighter.kt`

### Testing

The plugin uses the IntelliJ Platform testing framework. To run tests:

```bash
./gradlew test
```

## Troubleshooting

### Plugin Not Loading

- Ensure you're using IntelliJ IDEA 2023.2 or compatible version
- Check that the plugin .zip file is not corrupted
- Look for errors in **Help** → **Show Log in Explorer/Finder**

### Syntax Highlighting Not Working

- Verify the file has `.asm` extension
- Try closing and reopening the file
- Check **Settings** → **Editor** → **File Types** to ensure `.asm` is associated with CatASM

### Build Failures

- Ensure Java 17 or later is installed
- Clear Gradle cache: `./gradlew clean`
- Check internet connection (required to download dependencies)

## Contributing

Contributions are welcome! Please submit pull requests to the main [CatMachine repository](https://github.com/Serble/CatMachine).

## License

See the main repository's LICENSE file.

## Version History

- **1.0.0** (2024): Initial release
  - Basic syntax highlighting
  - File type recognition
  - Support for all CatASM instructions and directives
