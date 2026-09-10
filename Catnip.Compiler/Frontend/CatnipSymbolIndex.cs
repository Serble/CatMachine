using Catnip.Compiler.Ast;

namespace Catnip.Compiler.Frontend;

public enum CatnipSymbolKind {
    Function,
    Struct,
    StructField,
    Global,
    Local,
    Parameter,
    BinaryGlobal
}

/// <param name="Line">
/// 0-based line number, matching how editors and the Language Server Protocol count lines.
/// <see cref="FileInformation"/> counts from 1, so it is converted on the way in.
/// </param>
/// <param name="Column">0-based column number.</param>
public sealed record CatnipSymbolDefinition(
    string Name,
    CatnipSymbolKind Kind,
    string File,
    int Line,
    int Column,
    int EndColumn,
    string? ContainerName = null,
    string? Detail = null);

public sealed class CatnipSymbolIndex {
    private readonly Dictionary<string, List<CatnipSymbolDefinition>> _definitionsByName = new(StringComparer.Ordinal);
    private readonly List<CatnipSymbolDefinition> _allDefinitions = [];

    public IReadOnlyList<CatnipSymbolDefinition> Definitions => _allDefinitions;

    public void Add(CatnipSymbolDefinition definition) {
        _allDefinitions.Add(definition);
        if (!_definitionsByName.TryGetValue(definition.Name, out List<CatnipSymbolDefinition>? defs)) {
            defs = [];
            _definitionsByName[definition.Name] = defs;
        }
        defs.Add(definition);
    }

    public IReadOnlyList<CatnipSymbolDefinition> FindByName(string name) {
        if (_definitionsByName.TryGetValue(name, out List<CatnipSymbolDefinition>? defs)) {
            return defs;
        }
        return [];
    }

    public CatnipSymbolDefinition? FindBestDefinition(string name, string preferredFile, int preferredLine) {
        IReadOnlyList<CatnipSymbolDefinition> defs = FindByName(name);
        if (defs.Count == 0) return null;

        int SymbolRank(CatnipSymbolKind kind) {
            return kind switch {
                CatnipSymbolKind.Local => 0,
                CatnipSymbolKind.Parameter => 1,
                CatnipSymbolKind.Global => 2,
                CatnipSymbolKind.Function => 3,
                CatnipSymbolKind.Struct => 4,
                CatnipSymbolKind.StructField => 5,
                CatnipSymbolKind.BinaryGlobal => 6,
                _ => 100
            };
        }

        return defs
            .OrderBy(d => d.File.Equals(preferredFile, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(d => d.File.Equals(preferredFile, StringComparison.OrdinalIgnoreCase) && d.Line <= preferredLine ? 0 : 1)
            .ThenByDescending(d => d.File.Equals(preferredFile, StringComparison.OrdinalIgnoreCase) ? d.Line : -1)
            .ThenBy(d => SymbolRank(d.Kind))
            .FirstOrDefault();
    }

    public static CatnipSymbolIndex Build(CatProgram program) {
        return BuildFromParsedElements(
            [
                ..program.Structs,
                ..program.Functions,
                ..program.TopLevelStatements
            ],
            program.BinaryGlobals);
    }

    public static CatnipSymbolIndex BuildFromParsedElements(
        IReadOnlyList<ParsedElement> parsedElements,
        IReadOnlyList<BinaryGlobal> binaryGlobals) {
        CatnipSymbolIndex index = new();

        foreach (ParsedElement element in parsedElements) {
            switch (element) {
                case Struct structure: {
                    if (structure.FileInformation != null) {
                        index.Add(FromFileInformation(
                            structure.Name,
                            CatnipSymbolKind.Struct,
                            structure.FileInformation,
                            null,
                            $"struct {structure.Name}"));
                    }

                    foreach (VarNameSize field in structure.Fields) {
                        // A field is located at its own line, so that jumping to `Thing#b` lands
                        // on `b` rather than on the struct header; the header is only the
                        // fallback for an AST built without field positions.
                        FileInformation? position = field.FileInformation ?? structure.FileInformation;
                        if (position == null) continue;
                        index.Add(FromFileInformation(
                            $"{structure.Name}#{field.Name}",
                            CatnipSymbolKind.StructField,
                            position,
                            structure.Name,
                            $"field {field.Name}",
                            field.Name.Length));
                    }

                    break;
                }

                case Function function: {
                    if (function.FileInformation != null) {
                        index.Add(FromFileInformation(
                            function.Name,
                            CatnipSymbolKind.Function,
                            function.FileInformation,
                            null,
                            $"fun {function.Name}"));
                    }

                    foreach (VarNameSize parameter in function.Parameters) {
                        FileInformation? position = parameter.FileInformation ?? function.FileInformation;
                        if (position == null) continue;
                        index.Add(FromFileInformation(
                            parameter.Name,
                            CatnipSymbolKind.Parameter,
                            position,
                            function.Name,
                            $"{parameter.Name}:{parameter.Size}"));
                    }

                    foreach (Statement statement in function.Statements) {
                        AddStatementSymbols(index, function.Name, statement);
                    }

                    break;
                }

                case Statement statement:
                    AddStatementSymbols(index, null, statement);
                    break;
            }
        }
        
        foreach (BinaryGlobal binaryGlobal in binaryGlobals) {
            index.Add(new CatnipSymbolDefinition(
                binaryGlobal.Name,
                CatnipSymbolKind.BinaryGlobal,
                binaryGlobal.FileName ?? "unknown",
                0,
                0,
                binaryGlobal.Name.Length,
                null,
                "binary global"));
        }

        return index;
    }

    private static void AddStatementSymbols(CatnipSymbolIndex index, string? containerName, Statement statement) {
        switch (statement) {
            case GlobalDeclaration globalDeclaration when globalDeclaration.FileInformation != null:
                index.Add(FromFileInformation(
                    globalDeclaration.Name,
                    CatnipSymbolKind.Global,
                    globalDeclaration.FileInformation,
                    containerName,
                    $"global {globalDeclaration.Name}:{globalDeclaration.Size}"));
                break;

            case LocalDeclaration localDeclaration when localDeclaration.FileInformation != null:
                index.Add(FromFileInformation(
                    localDeclaration.Name,
                    CatnipSymbolKind.Local,
                    localDeclaration.FileInformation,
                    containerName,
                    $"let {localDeclaration.Name}:{localDeclaration.Size}"));
                break;

            case StatementBlock statementBlock:
                foreach (Statement child in statementBlock.Statements) {
                    AddStatementSymbols(index, containerName, child);
                }
                break;

            case IfStatement ifStatement:
                AddStatementSymbols(index, containerName, ifStatement.ThenStatements);
                AddStatementSymbols(index, containerName, ifStatement.ElseStatements);
                break;

            case WhileStatement whileStatement:
                AddStatementSymbols(index, containerName, whileStatement.BodyStatements);
                break;

            case SwitchStatement switchStatement:
                foreach ((_, Statement caseStatement) in switchStatement.Cases) {
                    AddStatementSymbols(index, containerName, caseStatement);
                }
                AddStatementSymbols(index, containerName, switchStatement.DefaultStatements);
                break;
        }
    }

    /// <param name="spanLength">
    /// How many characters the definition covers at its position: the name's length, unless the
    /// name is a qualified one like <c>Thing#b</c> positioned at the bare field, in which case
    /// it is the field's.
    /// </param>
    private static CatnipSymbolDefinition FromFileInformation(
        string name,
        CatnipSymbolKind kind,
        FileInformation fileInformation,
        string? containerName,
        string? detail,
        int? spanLength = null) {
        int column = Math.Max(0, fileInformation.Column - 1);
        // FileInformation is 1-based; CatnipSymbolDefinition is 0-based like the LSP, so without
        // this conversion go-to-definition lands one line below the actual definition.
        int line = Math.Max(0, fileInformation.Line - 1);
        return new CatnipSymbolDefinition(
            name,
            kind,
            fileInformation.File,
            line,
            column,
            column + (spanLength ?? name.Length),
            containerName,
            detail);
    }
}
