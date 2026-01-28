// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using CatC.Compiler.Tokenisation;

string t = File.ReadAllText("../../../test.cc");
Tokeniser tokeniser = new("test.cc", t);

IToken[] tokens = tokeniser.Tokenise();
File.WriteAllText("tokens.json", JsonSerializer.Serialize(tokens, new JsonSerializerOptions {
    WriteIndented = true
}));
