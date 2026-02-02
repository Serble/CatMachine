using System.Text.Json;
using CatAssembler.Assembler;
using CatAssembler.Parser;
using CatC.Compiler;
using CatC.Compiler.Analysis;
using CatC.Compiler.Ast;
using CatC.Compiler.CodeGen;
using CatC.Compiler.Parser;
using CatData;
using Newtonsoft.Json;
using Sprache;
using JsonSerializer = System.Text.Json.JsonSerializer;
using ParseException = CatAssembler.Exceptions.ParseException;

string t = File.ReadAllText("../../../test.cc");

Preprocesser preprocesser = new("test.cc", t);
ParsedElement[] tokens = CodeParser.Program.Parse(string.Join('\n', preprocesser.Process()));
Analyser analyser = new(tokens);
CatProgram program = analyser.Analyse();

CodeGenerator gen = new(program);
gen.Generate();
Console.WriteLine("Generated assembly");

try {
    Tokeniser tokeniser = new("main", File.ReadAllLines("output.asm"));
    CatAssembler.Analysis.Analyser asmAnalyser = new(tokeniser.Tokenise());
    (IOutputSegment[] segments, Dictionary<string, string> constants, DebugTable debugSymbols) = asmAnalyser.Analyse();
    Assembler assembler = new(segments, constants);
    
    FileStream outputStream = new("out.a", FileMode.Create, FileAccess.Write);
    assembler.WriteTo(outputStream);
    outputStream.Close();
    Console.WriteLine("Assembled successfully to out.a");
    
    FileStream debugSymbolsStream = new("out.a.debug", FileMode.Create, FileAccess.Write);
    // write json
    using StreamWriter writer = new(debugSymbolsStream);
    writer.Write(JsonSerializer.Serialize(debugSymbols, new JsonSerializerOptions {
        WriteIndented = true
    }));
}
catch (ParseException e) {
    Console.WriteLine("Error: " + e.Message);
}

File.WriteAllText("tokens.json", JsonConvert.SerializeObject(tokens, Formatting.Indented));
