using System.Text.Json;
using CatAssembler.Assembler;
using CatAssembler.Parser;
using CatC.Compiler;
using CatC.Compiler.Analysis;
using CatC.Compiler.Ast;
using CatC.Compiler.CodeGen;
using CatC.Compiler.Parser;
using CatData;
using JsonSerializer = System.Text.Json.JsonSerializer;

if (args.Length < 1) {
    Console.WriteLine("Usage: ./CatAssembler <input file> [options]");
    return 1;
}
string inputFilePath = args[0];
if (!File.Exists(inputFilePath)) {
    Console.WriteLine("Input file not found: " + inputFilePath);
    return 1;
}

string outputFile = "output.bin";
string? asmOutputFile = null;
string? asmDebugFile = null;
for (int i = 1; i < args.Length; i++) {
    switch (args[i].ToLower()) {
        case "--output" or "-o":
            if (i + 1 < args.Length) {
                outputFile = args[i + 1];
                i++;
            } else {
                Console.WriteLine("Missing value for --output flag.");
            }
            break;
        
        case "--asm-output" or "-s":
            if (i + 1 < args.Length) {
                asmOutputFile = args[i + 1];
                i++;
            } else {
                Console.WriteLine("Missing value for --asm-output flag.");
            }
            break;
        
        case "--asm-debug" or "-d":
            if (i + 1 < args.Length) {
                asmDebugFile = args[i + 1];
                i++;
            } else {
                Console.WriteLine("Missing value for --asm-debug flag.");
            }
            break;
        
        case "--help" or "-h":
            Console.WriteLine("Usage: ./CatC.Compiler <input file> [options]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --output, -o <file>        Specify output binary file (default: output.bin)");
            Console.WriteLine("  --asm-output, -s <file>    Specify output assembly file");
            Console.WriteLine("  --asm-debug, -d <file>     Specify output assembly debug symbols file");
            Console.WriteLine("  --help, -h                 Show this help message");
            return 0;
        
        default:
            Console.WriteLine($"Unknown flag: {args[i]}");
            break;
    }
}

// change current directory to be at the input file's directory
Directory.SetCurrentDirectory(Path.GetDirectoryName(Path.GetFullPath(inputFilePath))!);

string fileName = Path.GetFileName(inputFilePath);
Preprocesser preprocesser = new(fileName, File.ReadAllText(inputFilePath));
(string[] lines, (string File, int Line)[] lineMappings) = preprocesser.Process();
if (lines.Length != lineMappings.Length) {
    throw new Exception("Preprocessor returned mismatched line and mapping counts");
}

string asm;
try {
    ParsedElement[] tokens = CodeParser.ParseCode(string.Join('\n', lines), lineMappings);

    Analyser analyser = new(tokens);
    CatProgram program = analyser.Analyse();

    CodeGenerator gen = new(program);
    asm = gen.Generate();

    if (asmOutputFile != null) {
        File.WriteAllText(asmOutputFile, asm);
    }

    Console.WriteLine("Generated assembly" + (asmOutputFile != null ? $" to {asmOutputFile}" : ""));
}
catch (CompilationFailureException e) {
    Console.Error.WriteLine("Compilation Error: " + e.Message);
    if (e.Context != null) {
        Console.Error.WriteLine("-------------------------------------------------");
        Console.Error.WriteLine(e.Context);
        Console.Error.WriteLine("-------------------------------------------------");
    }
    return 1;
}


// Assembly phase:
// we've done our job as the compiler, now hand off to the assembler
// If assembler errors let it bubble because it's a bug

Tokeniser tokeniser = new("main", asm);
CatAssembler.Analysis.Analyser asmAnalyser = new(tokeniser.Tokenise());
(IOutputSegment[] segments, Dictionary<string, string> constants, DebugTable debugSymbols) = asmAnalyser.Analyse();
Assembler assembler = new(segments, constants);
    
FileStream outputStream = new(outputFile, FileMode.Create, FileAccess.Write);
assembler.WriteTo(outputStream);
outputStream.Close();
Console.WriteLine($"Assembled successfully to {outputFile}");

if (asmDebugFile != null) {
    FileStream debugSymbolsStream = new(asmDebugFile, FileMode.Create, FileAccess.Write);
    // write json
    using StreamWriter writer = new(debugSymbolsStream);
    writer.Write(JsonSerializer.Serialize(debugSymbols, new JsonSerializerOptions {
        WriteIndented = true
    }));
    Console.WriteLine("Wrote ASM debug symbols to " + asmDebugFile);
}
return 0;
