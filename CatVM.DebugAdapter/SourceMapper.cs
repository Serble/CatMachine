using System.Text.Json.Nodes;
using CatData;
using CatVM.DebugAdapter.Protocol;
using CatVM.Debugger;

namespace CatVM.DebugAdapter;

/// <summary>
/// Bridges the file names recorded in debug symbols (which may be relative include strings or
/// whatever a <c>#line</c> directive said) and the absolute paths a client uses.
/// </summary>
public sealed class SourceMapper {
    private readonly DebugTableIndex _index;
    private readonly string[] _baseDirs;
    private readonly bool _assemblyView;
    private readonly Dictionary<string, ResolvedFile> _resolved = new(StringComparer.OrdinalIgnoreCase);

    private sealed record ResolvedFile(string Name, string? AbsolutePath);

    public SourceMapper(DebugTableIndex index, IEnumerable<string?> baseDirs, bool assemblyView) {
        _index = index;
        _assemblyView = assemblyView;
        _baseDirs = baseDirs
            .Where(d => !string.IsNullOrEmpty(d))
            .Select(d => Path.GetFullPath(d!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public bool AssemblyView => _assemblyView;

    private ResolvedFile Resolve(string symbolFile) {
        if (_resolved.TryGetValue(symbolFile, out ResolvedFile? cached)) return cached;

        string? absolute = null;
        if (symbolFile.Length != 0) {
            if (Path.IsPathRooted(symbolFile)) {
                if (File.Exists(symbolFile)) absolute = Path.GetFullPath(symbolFile);
            }
            else {
                foreach (string baseDir in _baseDirs) {
                    string candidate = Path.GetFullPath(Path.Combine(baseDir, symbolFile));
                    if (!File.Exists(candidate)) continue;
                    absolute = candidate;
                    break;
                }
            }
        }

        ResolvedFile resolved = new(Path.GetFileName(symbolFile), absolute);
        _resolved[symbolFile] = resolved;
        return resolved;
    }

    /// <summary>The location a client should show for an instruction, honouring the source view.</summary>
    public (string File, int Line) LocationOf(DebugSymbol symbol) {
        return _assemblyView ? (symbol.File, symbol.Line) : symbol.EffectiveLocation;
    }

    /// <summary>DAP <c>Source</c> for the location <see cref="LocationOf"/> reports.</summary>
    public JsonObject SourceFor(DebugSymbol symbol) {
        if (!_assemblyView && symbol.SourceFile != null) {
            JsonObject source = SourceObject(symbol.SourceFile, "catnip");
            // Clients that show nested sources can offer the generated assembly too.
            source["sources"] = J.Arr([SourceObject(symbol.File, "generated assembly")]);
            return source;
        }

        return SourceObject(symbol.File, symbol.SourceFile != null ? "generated assembly" : "catasm");
    }

    public JsonObject SourceObject(string symbolFile, string origin) {
        ResolvedFile resolved = Resolve(symbolFile);
        JsonObject source = J.Obj(
            ("name", resolved.Name.Length != 0 ? resolved.Name : symbolFile),
            ("origin", origin));
        if (resolved.AbsolutePath != null) {
            source["path"] = resolved.AbsolutePath;
        }
        else if (symbolFile.Length != 0) {
            // Not on disk where we looked; hand the client what the symbols said and let it try.
            source["path"] = symbolFile;
        }

        return source;
    }

    /// <summary>
    /// How well a symbol's file matches a path the client sent: 3 = same absolute file,
    /// 2 = the client path ends with the symbol's relative path, 1 = same bare file name,
    /// 0 = no match.
    /// </summary>
    public int MatchStrength(string symbolFile, string clientPath) {
        if (symbolFile.Length == 0 || clientPath.Length == 0) return 0;

        ResolvedFile resolved = Resolve(symbolFile);
        string clientFull = SafeFullPath(clientPath);
        if (resolved.AbsolutePath != null && string.Equals(resolved.AbsolutePath, clientFull, StringComparison.OrdinalIgnoreCase)) {
            return 3;
        }

        string normalisedSymbol = symbolFile.Replace('\\', '/').TrimStart('.', '/');
        string normalisedClient = clientFull.Replace('\\', '/');
        if (normalisedSymbol.Contains('/') &&
            normalisedClient.EndsWith("/" + normalisedSymbol, StringComparison.OrdinalIgnoreCase)) {
            return 2;
        }

        if (Path.GetFileName(symbolFile).Equals(Path.GetFileName(clientPath), StringComparison.OrdinalIgnoreCase)) {
            return 1;
        }

        return 0;
    }

    private static string SafeFullPath(string path) {
        try {
            return Path.GetFullPath(path);
        }
        catch (Exception) {
            return path;
        }
    }

    /// <summary>
    /// Instructions on <paramref name="line"/> of the client's file. When several symbol files
    /// match, the best-matching ones win so <c>a/main.cat</c> and <c>b/main.cat</c> don't
    /// collide; a genuine tie is reported as ambiguous.
    /// </summary>
    public LineResolution ResolveClientLine(string clientPath, int line) {
        List<(DebugSymbol symbol, string file, int strength)> matches = [];
        int best = 0;
        foreach (DebugSymbol symbol in _index.Table.Symbols) {
            (string file, int symbolLine) = LocationOf(symbol);
            if (symbolLine != line) continue;
            int strength = MatchStrength(file, clientPath);
            if (strength == 0) continue;
            matches.Add((symbol, file, strength));
            if (strength > best) best = strength;
        }

        DebugSymbol[] symbols = matches.Where(m => m.strength == best).Select(m => m.symbol).ToArray();
        string[] files = matches.Where(m => m.strength == best).Select(m => m.file)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new LineResolution(symbols, files);
    }

    /// <summary>Every file the symbols mention, as DAP sources.</summary>
    public JsonArray LoadedSources() {
        List<JsonNode?> sources = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (DebugSymbol symbol in _index.SymbolsByAddress) {
            (string file, _) = LocationOf(symbol);
            if (file.Length == 0 || !seen.Add(file)) continue;
            sources.Add(SourceFor(symbol));
        }

        if (!_assemblyView) {
            foreach (string file in _index.AssemblyFiles) {
                if (!seen.Add(file)) continue;
                sources.Add(SourceObject(file, "generated assembly"));
            }
        }

        return J.Arr(sources);
    }
}
