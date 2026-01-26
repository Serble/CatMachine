using CatAssembler.Analysis;
using CatAssembler.Assembler;
using CatAssembler.Exceptions;
using Tokeniser = CatAssembler.Parser.Tokeniser;

if (args.Length < 1) {
    Console.WriteLine("Usage: ./CatAssembler <input file> [options]");
    return;
}
string inputFilePath = args[0];
if (!File.Exists(inputFilePath)) {
    Console.WriteLine("Input file not found: " + inputFilePath);
    return;
}

string outputFile = "output.bin";
for (int i = 1; i < args.Length; i++) {
    switch (args[i]) {
        case "--output" or "-o":
            if (i + 1 < args.Length) {
                outputFile = args[i + 1];
                i++;
            } else {
                Console.WriteLine("Missing value for --output flag.");
            }
            break;
        
        default:
            Console.WriteLine($"Unknown flag: {args[i]}");
            break;
    }
}

// change current directory to be at the input file's directory
Directory.SetCurrentDirectory(Path.GetDirectoryName(Path.GetFullPath(inputFilePath))!);

try {
    Tokeniser tokeniser = new(Path.GetFileName(inputFilePath), File.ReadAllLines(Path.GetFileName(inputFilePath)));
    Analyser analyser = new(tokeniser.Tokenise());
    (IOutputSegment[] segments, Dictionary<string, string> constants) = analyser.Analyse();
    Assembler assembler = new(segments, constants);
    
    FileStream outputStream = new(outputFile, FileMode.Create, FileAccess.Write);
    assembler.WriteTo(outputStream);
    outputStream.Close();
    Console.WriteLine("Assembled successfully to " + outputFile);
}
catch (ParseException e) {
    Console.WriteLine("Error: " + e.Message);
}


