using System.Text.Json;
using CatAssembler.Analysis;
using CatAssembler.Assembler;
using CatAssembler.Parser;
using CatData;

namespace CatVM.Testing.Dap;

/// <summary>
/// A small program assembled in-process into a temp directory: ROM plus <c>.debug</c> file,
/// ready for the adapter (or a <see cref="CatVM.Debugger.CatDebugger"/>) to load.
/// </summary>
public sealed class DapFixture : IDisposable {
    public string Directory { get; }
    public string SourcePath { get; }
    public string RomPath { get; }
    public string DebugPath => RomPath + ".debug";
    public DebugTable Table { get; }
    public byte[] Rom { get; }

    public DapFixture(string fileName, string source) {
        Directory = Path.Combine(Path.GetTempPath(), "catvm-dap-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        SourcePath = Path.Combine(Directory, fileName);
        RomPath = Path.Combine(Directory, Path.ChangeExtension(fileName, ".bin"));
        File.WriteAllText(SourcePath, source);

        (Rom, Table) = Assemble(SourcePath, source);
        File.WriteAllBytes(RomPath, Rom);
        File.WriteAllText(DebugPath, Table.ToJson(new JsonSerializerOptions { WriteIndented = true }));
    }

    public static (byte[] rom, DebugTable table) Assemble(string fileName, string source) {
        Token[] tokens = new Tokeniser(fileName, source).Tokenise();
        (IOutputSegment[] segments, Dictionary<string, string> constants, DebugTable table) = new Analyser(tokens).Analyse();
        using MemoryStream ms = new();
        new Assembler(segments, constants).WriteTo(ms);
        return (ms.ToArray(), table);
    }

    /// <summary>Address of the first instruction on <paramref name="line"/> of the fixture source.</summary>
    public uint AddressOfLine(int line) {
        return (uint)Table.Symbols.Where(s => s.Line == line).Min(s => s.FilePos);
    }

    public uint Label(string name) => Table.Labels[name];

    public void Dispose() {
        try {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch {
            // best effort
        }
    }

    // Line numbers are load-bearing: tests refer to them.
    public const string CallProgram =
        "main:\n" +               // 1
        "    mov r0, 1\n" +       // 2
        "    mov r1, 2\n" +       // 3
        "    call add_regs\n" +   // 4
        "    mov r2, r0\n" +      // 5
        "loop:\n" +               // 6
        "    jmp loop\n" +        // 7
        "add_regs:\n" +           // 8
        "    add r0, r1\n" +      // 9
        "    ret\n";              // 10

    public const string CatnipMappedProgram =
        "#line 10, \"fixture.nip\"\n" +  // 1
        "main:\n" +                       // 2
        "    mov r0, 1\n" +               // 3   -> nip 10
        "    mov r1, 2\n" +               // 4   -> nip 10
        "#line 11, \"fixture.nip\"\n" +  // 5
        "    call add_regs\n" +           // 6   -> nip 11
        "#line 12, \"fixture.nip\"\n" +  // 7
        "    mov r2, r0\n" +              // 8   -> nip 12
        "#line 13, \"fixture.nip\"\n" +  // 9
        "loop:\n" +                       // 10
        "    jmp loop\n" +                // 11  -> nip 13
        "#line 20, \"fixture.nip\"\n" +  // 12
        "add_regs:\n" +                   // 13
        "    add r0, r1\n" +              // 14  -> nip 20
        "#line 21, \"fixture.nip\"\n" +  // 15
        "    ret\n";                      // 16  -> nip 21

    public const string FaultProgram =
        "main:\n" +               // 1
        "    mov r0, 10\n" +      // 2
        "    mov r1, 0\n" +       // 3
        "    udiv r0, r1\n" +     // 4  divide by zero
        "    nop\n" +             // 5
        "loop:\n" +               // 6
        "    jmp loop\n";         // 7

    public const string PrintProgram =
        "main:\n" +               // 1
        "    mov r1, 42\n" +      // 2
        "    int 0x90\n" +        // 3  print number (test interrupt)
        "loop:\n" +               // 4
        "    jmp loop\n";         // 5
}
