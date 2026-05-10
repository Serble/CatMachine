using CatLLVM.CodeGen;
using CatLLVM.IR;

if (args.Length < 1) {
    Console.WriteLine("Usage: ./CatLLVM <input.ll> [more.ll ...] [-o output.cat]");
    Console.WriteLine();
    Console.WriteLine("Reads one or more LLVM IR text files, links them into a single");
    Console.WriteLine("module and emits Cat assembly that can be fed straight into");
    Console.WriteLine("CatAssembler to produce a CatVM ROM.");
    return 1;
}

List<string> inputs = [];
string? outputPath = null;
for (int i = 0; i < args.Length; i++) {
    switch (args[i]) {
        case "-o" or "--output":
            if (i + 1 >= args.Length) {
                Console.Error.WriteLine("Missing value for -o");
                return 1;
            }
            outputPath = args[++i];
            break;
        default:
            if (args[i].StartsWith("-")) {
                Console.Error.WriteLine($"Unknown flag: {args[i]}");
                return 1;
            }
            inputs.Add(args[i]);
            break;
    }
}

if (inputs.Count == 0) {
    Console.Error.WriteLine("No input files provided.");
    return 1;
}
foreach (string p in inputs) {
    if (!File.Exists(p)) {
        Console.Error.WriteLine($"Input file not found: {p}");
        return 1;
    }
}
outputPath ??= Path.ChangeExtension(inputs[0], ".cat");

try {
    // Parse and merge all input modules into one. Later files win on
    // duplicate function/global names (so a user .ll can override a libc
    // symbol if needed). Declarations are dropped when a later definition
    // exists.
    IrModule merged = new();
    Dictionary<string, IrFunction> fnByName = new();
    Dictionary<string, IrGlobalDecl> gByName = new();

    foreach (string p in inputs) {
        IrModule m = IRParser.Parse(File.ReadAllText(p));
        foreach (IrGlobalDecl g in m.Globals) gByName[g.Name] = g;
        foreach (IrFunction f in m.Functions) {
            if (fnByName.TryGetValue(f.Name, out IrFunction? existing)) {
                // Prefer the one with a body; if both have bodies, last wins.
                if (existing.IsDeclaration || !f.IsDeclaration) fnByName[f.Name] = f;
            } else {
                fnByName[f.Name] = f;
            }
        }
        Console.WriteLine($"Parsed {p}: {m.Globals.Count} globals, {m.Functions.Count} functions");
    }
    merged.Globals.AddRange(gByName.Values);
    merged.Functions.AddRange(fnByName.Values);

    CodeGenerator gen = new(merged, string.Join(" + ", inputs.Select(Path.GetFileName)));
    string asm = gen.Generate();

    File.WriteAllText(outputPath, asm);
    Console.WriteLine($"Wrote {outputPath}");
    return 0;
} catch (ParseException px) {
    Console.Error.WriteLine($"parse error: {px.Message}");
    return 2;
} catch (NotSupportedException nsx) {
    Console.Error.WriteLine($"unsupported feature: {nsx.Message}");
    return 3;
}

