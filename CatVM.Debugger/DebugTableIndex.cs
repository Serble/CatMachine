using System.Globalization;
using CatData;

namespace CatVM.Debugger;

/// <summary>
/// Indexed view over a <see cref="DebugTable"/> so the queries a debugger front-end makes on every
/// instruction (address → symbol) and on every breakpoint (file:line → address, label → address)
/// don't have to scan the whole symbol list.
/// </summary>
public sealed class DebugTableIndex {
    public DebugTable Table { get; }

    /// <summary>
    /// Symbols ordered by <see cref="DebugSymbol.FilePos"/>, one per distinct address. When
    /// several symbols share an address (a macro that expanded to nothing, for example) the
    /// first one in the table wins.
    /// </summary>
    public DebugSymbol[] SymbolsByAddress { get; }

    /// <summary>Every distinct file a symbol reports through <see cref="DebugSymbol.EffectiveLocation"/>.</summary>
    public IReadOnlyCollection<string> EffectiveFiles { get; }

    /// <summary>Every distinct assembly file (<see cref="DebugSymbol.File"/>).</summary>
    public IReadOnlyCollection<string> AssemblyFiles { get; }

    private readonly Dictionary<uint, int> _indexByAddress = [];
    private readonly Dictionary<uint, string> _codeLabelByAddress = [];
    private readonly uint[] _codeLabelAddresses;
    private readonly string[] _codeLabelNames;

    public DebugTableIndex(DebugTable table) {
        Table = table;

        List<DebugSymbol> ordered = [.. table.Symbols];
        // Stable sort so equal addresses keep table order.
        ordered = ordered.OrderBy(s => s.FilePos).ToList();

        List<DebugSymbol> distinct = new(ordered.Count);
        foreach (DebugSymbol symbol in ordered) {
            uint addr = (uint)symbol.FilePos;
            if (_indexByAddress.ContainsKey(addr)) continue;
            _indexByAddress[addr] = distinct.Count;
            distinct.Add(symbol);
        }
        SymbolsByAddress = distinct.ToArray();

        HashSet<string> effectiveFiles = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> assemblyFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (DebugSymbol symbol in table.Symbols) {
            if (symbol.File.Length != 0) assemblyFiles.Add(symbol.File);
            string effective = symbol.EffectiveLocation.File;
            if (effective.Length != 0) effectiveFiles.Add(effective);
        }
        EffectiveFiles = effectiveFiles;
        AssemblyFiles = assemblyFiles;

        // Labels mixes real code labels with #define constants; only the ones that point at an
        // instruction are useful as function names / reverse lookups.
        List<(uint addr, string name)> codeLabels = [];
        foreach ((string name, uint addr) in table.Labels) {
            if (!_indexByAddress.ContainsKey(addr)) continue;
            codeLabels.Add((addr, name));
            _codeLabelByAddress.TryAdd(addr, name);
        }
        codeLabels.Sort((a, b) => a.addr.CompareTo(b.addr));
        _codeLabelAddresses = codeLabels.Select(l => l.addr).ToArray();
        _codeLabelNames = codeLabels.Select(l => l.name).ToArray();
    }

    /// <summary>Exact lookup: the symbol for the instruction starting at <paramref name="addr"/>.</summary>
    public bool TryGetSymbolAt(uint addr, out DebugSymbol symbol) {
        if (_indexByAddress.TryGetValue(addr, out int index)) {
            symbol = SymbolsByAddress[index];
            return true;
        }

        symbol = null!;
        return false;
    }

    public DebugSymbol? GetSymbolAt(uint addr) {
        return TryGetSymbolAt(addr, out DebugSymbol symbol) ? symbol : null;
    }

    /// <summary>
    /// Index into <see cref="SymbolsByAddress"/> of the last symbol whose address is at or before
    /// <paramref name="addr"/>, or -1 when <paramref name="addr"/> precedes every symbol.
    /// </summary>
    public int IndexAtOrBefore(uint addr) {
        int lo = 0, hi = SymbolsByAddress.Length - 1, result = -1;
        while (lo <= hi) {
            int mid = (lo + hi) / 2;
            if ((uint)SymbolsByAddress[mid].FilePos <= addr) {
                result = mid;
                lo = mid + 1;
            }
            else {
                hi = mid - 1;
            }
        }

        return result;
    }

    /// <summary>
    /// Byte length of the instruction at <paramref name="index"/> in <see cref="SymbolsByAddress"/>,
    /// derived from the next symbol's address. -1 for the last symbol, whose length is unknown.
    /// </summary>
    public int InstructionLength(int index) {
        if (index < 0 || index >= SymbolsByAddress.Length - 1) return -1;
        return SymbolsByAddress[index + 1].FilePos - SymbolsByAddress[index].FilePos;
    }

    /// <summary>
    /// Find the instructions on a source line. A bare line number is ambiguous as soon as a
    /// project uses <c>#include</c>, so <paramref name="fileFilter"/> (a full path or a bare
    /// file name) narrows it down; when it is null or empty every file is considered and the
    /// result reports whether the line was found in more than one.
    /// </summary>
    /// <param name="assemblyView">
    /// Match against the assembly location (<see cref="DebugSymbol.File"/>/<see cref="DebugSymbol.Line"/>)
    /// rather than <see cref="DebugSymbol.EffectiveLocation"/>.
    /// </param>
    public LineResolution ResolveLine(string? fileFilter, int line, bool assemblyView = false) {
        List<DebugSymbol> matches = [];
        foreach (DebugSymbol symbol in Table.Symbols) {
            (string symbolFile, int symbolLine) = assemblyView
                ? (symbol.File, symbol.Line)
                : symbol.EffectiveLocation;
            if (symbolLine != line) continue;
            if (!string.IsNullOrEmpty(fileFilter) && !FileMatches(symbolFile, fileFilter)) continue;
            matches.Add(symbol);
        }

        string[] files = matches
            .Select(m => assemblyView ? m.File : m.EffectiveLocation.File)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new LineResolution(matches.ToArray(), files);
    }

    public bool TryGetLabel(string name, out uint address) {
        return Table.Labels.TryGetValue(name, out address);
    }

    /// <summary>True when <paramref name="name"/> is a label that points at an instruction.</summary>
    public bool IsCodeLabel(string name) {
        return Table.Labels.TryGetValue(name, out uint address) && _indexByAddress.ContainsKey(address);
    }

    /// <summary>The code label defined exactly at <paramref name="addr"/>, if any.</summary>
    public string? CodeLabelAt(uint addr) {
        return _codeLabelByAddress.GetValueOrDefault(addr);
    }

    /// <summary>
    /// The nearest code label at or before <paramref name="addr"/>: the "function" the address
    /// is in, as far as a flat label table can tell.
    /// </summary>
    public string? FunctionContaining(uint addr) {
        int lo = 0, hi = _codeLabelAddresses.Length - 1, result = -1;
        while (lo <= hi) {
            int mid = (lo + hi) / 2;
            if (_codeLabelAddresses[mid] <= addr) {
                result = mid;
                lo = mid + 1;
            }
            else {
                hi = mid - 1;
            }
        }

        return result < 0 ? null : _codeLabelNames[result];
    }

    /// <summary>Any label (code or constant) whose value is <paramref name="value"/>.</summary>
    public string? LabelWithValue(uint value) {
        if (_codeLabelByAddress.TryGetValue(value, out string? code)) return code;
        foreach ((string key, uint val) in Table.Labels) {
            if (val == value) return key;
        }

        return null;
    }

    /// <summary>
    /// Matches a debug symbol's file against a user-supplied filter, accepting either a full
    /// path or just the file name so `break line display.cat:42` works without typing the path.
    /// </summary>
    public static bool FileMatches(string symbolFile, string filter) {
        if (symbolFile.Equals(filter, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return Path.GetFileName(symbolFile).Equals(Path.GetFileName(filter), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Parses a decimal or <c>0x</c>-prefixed hexadecimal number.</summary>
    public static bool TryParseNumber(string str, out uint value) {
        str = str.Trim();
        if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) {
            return uint.TryParse(str[2..], NumberStyles.HexNumber, null, out value);
        }

        return uint.TryParse(str, out value);
    }
}

/// <summary>Result of <see cref="DebugTableIndex.ResolveLine"/>.</summary>
public readonly record struct LineResolution(DebugSymbol[] Matches, string[] Files) {
    public bool IsAmbiguous => Files.Length > 1;

    /// <summary>
    /// The lowest address on the line, so a breakpoint lands on its first instruction. Null when
    /// nothing matched or the match is ambiguous.
    /// </summary>
    public uint? Address => Matches.Length == 0 || IsAmbiguous ? null : (uint)Matches.Min(m => m.FilePos);
}
