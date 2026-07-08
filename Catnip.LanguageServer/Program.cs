using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Reflection;
using Catnip.Compiler;
using Catnip.Compiler.Frontend;

CatnipFrontendService frontendService = new();
Dictionary<string, DocumentState> documents = new(StringComparer.OrdinalIgnoreCase);
bool shutdownRequested = false;
string[] semanticTokenLegend = ["keyword", "comment", "string", "number", "operator", "function", "variable", "struct"];

using Stream input = Console.OpenStandardInput();
using Stream output = Console.OpenStandardOutput();

LogStderr("Catnip.LanguageServer started");
bool sawAnyMessage = false;

while (TryReadMessage(input, out string? payload)) {
    if (payload == null) break;

    // Per-message error isolation: a failure while handling one request must never
    // crash the whole server, otherwise the editor client sees the process die and
    // restarts it in a loop (appearing to be perpetually "starting").
    bool hasErrorId = false;
    JsonElement errorId = default;
    try {
        using JsonDocument document = JsonDocument.Parse(payload);
        JsonElement root = document.RootElement;
        if (!root.TryGetProperty("method", out JsonElement methodElement)) continue;

        string method = methodElement.GetString() ?? string.Empty;
        sawAnyMessage = true;
        JsonElement paramsElement = root.TryGetProperty("params", out JsonElement p) ? p : default;
        bool hasId = root.TryGetProperty("id", out JsonElement idElement);
        if (hasId) {
            hasErrorId = true;
            errorId = idElement.Clone();
        }
        if (VerboseStderrLoggingEnabled()) {
            LogStderr($"method={method} hasId={hasId}");
        }

        switch (method) {
            case "initialize":
                if (hasId) {
                    WriteResponse(output, idElement, new {
                        capabilities = new {
                            textDocumentSync = 2,
                            hoverProvider = true,
                            definitionProvider = true,
                            documentSymbolProvider = true,
                            completionProvider = new {
                                triggerCharacters = new[] { ".", "#", "$" }
                            },
                            documentOnTypeFormattingProvider = new {
                                firstTriggerCharacter = "}",
                                moreTriggerCharacter = new[] { "{" }
                            },
                            semanticTokensProvider = new {
                                legend = new {
                                    tokenTypes = semanticTokenLegend,
                                    tokenModifiers = Array.Empty<string>()
                                },
                                full = true
                            }
                        },
                        serverInfo = new {
                            name = "Catnip.LanguageServer",
                            version = "0.2.0"
                        }
                    });
                }
                break;

            case "initialized":
                break;

            case "shutdown":
                shutdownRequested = true;
                if (hasId) {
                    WriteResponse(output, idElement, (object?)null);
                }
                break;

            case "exit":
                return shutdownRequested ? 0 : 1;

            case "textDocument/didOpen":
                HandleDidOpen(paramsElement, frontendService, documents, output);
                break;

            case "textDocument/didChange":
                HandleDidChange(paramsElement, frontendService, documents, output);
                break;

            case "textDocument/didClose":
                HandleDidClose(paramsElement, documents, output);
                break;

            case "textDocument/definition":
                if (hasId) {
                    WriteResponse(output, idElement, HandleDefinition(paramsElement, documents));
                }
                break;

            case "textDocument/hover":
                if (hasId) {
                    WriteResponse(output, idElement, HandleHover(paramsElement, documents));
                }
                break;

            case "textDocument/documentSymbol":
                if (hasId) {
                    WriteResponse(output, idElement, HandleDocumentSymbol(paramsElement, documents));
                }
                break;

            case "textDocument/completion":
                if (hasId) {
                    WriteResponse(output, idElement, HandleCompletion(paramsElement, documents));
                }
                break;

            case "textDocument/semanticTokens/full":
                if (hasId) {
                    WriteResponse(output, idElement, HandleSemanticTokens(paramsElement, documents, semanticTokenLegend));
                }
                break;

            case "textDocument/onTypeFormatting":
                if (hasId) {
                    WriteResponse(output, idElement, HandleOnTypeFormatting(paramsElement, documents));
                }
                break;

            default:
                if (hasId) {
                    WriteErrorResponse(output, idElement, -32601, $"Method not found: {method}");
                }
                break;
        }
    }
    catch (Exception exception) {
        LogStderr("error handling message; server continuing");
        LogStderr(exception.ToString());
        if (hasErrorId) {
            try {
                WriteErrorResponse(output, errorId, -32603, "Internal error: " + exception.Message);
            }
            catch (Exception writeException) {
                LogStderr("failed to write error response");
                LogStderr(writeException.ToString());
            }
        }
    }
}

LogStderr(sawAnyMessage
    ? "stdin closed or stream ended after handling at least one message; exiting"
    : "stdin closed before any LSP message was received; exiting");

return 0;

static void HandleDidOpen(
    JsonElement parameters,
    CatnipFrontendService frontendService,
    Dictionary<string, DocumentState> documents,
    Stream output) {
    JsonElement textDocument = parameters.GetProperty("textDocument");
    string uri = textDocument.GetProperty("uri").GetString() ?? string.Empty;
    string text = textDocument.GetProperty("text").GetString() ?? string.Empty;
    if (string.IsNullOrEmpty(uri)) return;

    string filePath = UriToFilePath(uri);
    FrontendCompilationResult result = Analyse(frontendService, filePath, text);
    HashSet<string> publishedUris = PublishDiagnosticsForDocument(
        output,
        uri,
        result.Diagnostics,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    documents[uri] = new DocumentState(text, result, publishedUris);
}

static void HandleDidChange(
    JsonElement parameters,
    CatnipFrontendService frontendService,
    Dictionary<string, DocumentState> documents,
    Stream output) {
    JsonElement textDocument = parameters.GetProperty("textDocument");
    string uri = textDocument.GetProperty("uri").GetString() ?? string.Empty;
    if (string.IsNullOrEmpty(uri)) return;

    string text = documents.TryGetValue(uri, out DocumentState? existingState) ? existingState.Text : string.Empty;
    JsonElement changes = parameters.GetProperty("contentChanges");
    foreach (JsonElement change in changes.EnumerateArray()) {
        string changeText = change.GetProperty("text").GetString() ?? string.Empty;
        if (change.TryGetProperty("range", out JsonElement range)) {
            text = ApplyRangeChange(text, range, changeText);
            continue;
        }

        text = changeText;
    }

    string filePath = UriToFilePath(uri);
    FrontendCompilationResult result = Analyse(frontendService, filePath, text);
    HashSet<string> previousPublishedUris = existingState?.PublishedDiagnosticUris ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    HashSet<string> publishedUris = PublishDiagnosticsForDocument(output, uri, result.Diagnostics, previousPublishedUris);
    documents[uri] = new DocumentState(text, result, publishedUris);
}

static void HandleDidClose(JsonElement parameters, Dictionary<string, DocumentState> documents, Stream output) {
    string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString() ?? string.Empty;
    if (string.IsNullOrEmpty(uri)) return;

    if (documents.TryGetValue(uri, out DocumentState? state)) {
        foreach (string diagnosticsUri in state.PublishedDiagnosticUris) {
            PublishDiagnostics(output, diagnosticsUri, Array.Empty<CatnipDiagnostic>());
        }
    }

    documents.Remove(uri);
}

static object HandleDocumentSymbol(JsonElement parameters, Dictionary<string, DocumentState> documents) {
    string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString() ?? string.Empty;
    if (string.IsNullOrEmpty(uri)) return Array.Empty<object>();
    if (!documents.TryGetValue(uri, out DocumentState? state)) return Array.Empty<object>();

    string currentFile = UriToFilePath(uri);
    object[] symbols = GetEffectiveSymbols(state, currentFile)
        .Where(def => PathsMatch(def.File, currentFile))
        .Select(def => new {
            name = def.Name,
            kind = ToLspSymbolKind(def.Kind),
            location = new {
                uri,
                range = new {
                    start = new { line = def.Line, character = def.Column },
                    end = new { line = def.Line, character = def.EndColumn }
                }
            },
            containerName = def.ContainerName
        })
        .Cast<object>()
        .ToArray();

    return symbols;
}

static object HandleCompletion(JsonElement parameters, Dictionary<string, DocumentState> documents) {
    string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString() ?? string.Empty;
    List<object> items = [];

    foreach (string keyword in new[] { "fun", "struct", "global", "let", "return", "if", "else", "while", "switch", "case", "default" }) {
        items.Add(new { label = keyword, kind = 14, detail = "keyword" });
    }

    foreach (string directive in new[] { "#include", "#define", "#binary" }) {
        items.Add(new { label = directive, kind = 14, detail = "preprocessor directive" });
    }

    foreach (string register in new[] { "r0", "r1", "r2", "r3", "r4", "r5", "r6", "r7", "sp", "ip", "fl" }) {
        items.Add(new { label = register, kind = 21, detail = "register" });
    }

    if (!string.IsNullOrEmpty(uri) && documents.TryGetValue(uri, out DocumentState? state)) {
        string filePath = UriToFilePath(uri);
        foreach (CatnipSymbolDefinition definition in GetEffectiveSymbols(state, filePath)
                     .GroupBy(d => (d.Name, d.Kind))
                     .Select(group => group.First())) {
            items.Add(new {
                label = definition.Name,
                kind = definition.Kind switch {
                    CatnipSymbolKind.Function => 3,
                    CatnipSymbolKind.Struct => 22,
                    CatnipSymbolKind.StructField => 5,
                    _ => 6
                },
                detail = definition.Detail ?? definition.Kind.ToString()
            });
        }
    }

    return new { isIncomplete = false, items };
}

static object? HandleDefinition(JsonElement parameters, Dictionary<string, DocumentState> documents) {
    string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString() ?? string.Empty;
    if (string.IsNullOrEmpty(uri)) return null;
    if (!documents.TryGetValue(uri, out DocumentState? state)) return null;

    JsonElement position = parameters.GetProperty("position");
    int line = position.GetProperty("line").GetInt32();
    int character = position.GetProperty("character").GetInt32();
    string token = GetTokenAt(state.Text, line, character);
    if (string.IsNullOrWhiteSpace(token)) return null;

    string filePath = UriToFilePath(uri);
    CatnipSymbolDefinition? definition = FindDefinition(GetEffectiveSymbols(state, filePath), token, filePath, line);
    if (definition == null) return null;

    string targetUri = FilePathToUri(definition.File);
    return new[] {
        new {
            uri = targetUri,
            range = new {
                start = new { line = definition.Line, character = definition.Column },
                end = new { line = definition.Line, character = definition.EndColumn }
            }
        }
    };
}

static object? HandleHover(JsonElement parameters, Dictionary<string, DocumentState> documents) {
    string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString() ?? string.Empty;
    if (string.IsNullOrEmpty(uri)) return null;
    if (!documents.TryGetValue(uri, out DocumentState? state)) return null;

    JsonElement position = parameters.GetProperty("position");
    int line = position.GetProperty("line").GetInt32();
    int character = position.GetProperty("character").GetInt32();
    string token = GetTokenAt(state.Text, line, character);
    if (string.IsNullOrWhiteSpace(token)) return null;

    string filePath = UriToFilePath(uri);
    CatnipSymbolDefinition? definition = FindDefinition(GetEffectiveSymbols(state, filePath), token, filePath, line);
    if (definition == null) return null;

    string value = $"**{definition.Kind}** `{definition.Name}`";
    if (!string.IsNullOrWhiteSpace(definition.Detail)) {
        value += $"\n\n{definition.Detail}";
    }
    if (TryGetDocumentationForDefinition(definition, documents, out string? documentation) &&
        definition.Detail?.Contains("Documentation:", StringComparison.Ordinal) != true) {
        value += $"\n\nDocumentation:\n{documentation}";
    }

    return new {
        contents = new { kind = "markdown", value },
        range = new {
            start = new { line = definition.Line, character = definition.Column },
            end = new { line = definition.Line, character = definition.EndColumn }
        }
    };
}

static bool TryGetDocumentationForDefinition(
    CatnipSymbolDefinition definition,
    Dictionary<string, DocumentState> documents,
    out string? documentation) {
    documentation = null;
    if (!TryReadSymbolSourceText(definition.File, documents, out string? sourceText) ||
        sourceText == null) {
        return false;
    }

    string[] lines = sourceText.Split('\n');
    if (definition.Line < 0 || definition.Line >= lines.Length) {
        return false;
    }

    int declarationLine = definition.Line;
    if (!LineContainsSymbolName(lines[declarationLine], definition.Name) &&
        declarationLine - 1 >= 0 &&
        LineContainsSymbolName(lines[declarationLine - 1], definition.Name)) {
        declarationLine -= 1;
    }

    List<string> docLines = [];
    for (int line = declarationLine - 1; line >= 0; line--) {
        string trimmed = lines[line].TrimStart();
        if (!trimmed.StartsWith("///", StringComparison.Ordinal)) {
            break;
        }

        string text = trimmed.Length > 3 ? trimmed[3..].TrimStart() : string.Empty;
        docLines.Add(text);
    }

    if (docLines.Count == 0) {
        return false;
    }

    docLines.Reverse();
    documentation = string.Join('\n', docLines).Trim();
    return !string.IsNullOrWhiteSpace(documentation);
}

static bool LineContainsSymbolName(string line, string symbolName) {
    return Regex.IsMatch(
        line,
        $@"\b{Regex.Escape(symbolName)}\b",
        RegexOptions.CultureInvariant);
}

static bool TryReadSymbolSourceText(
    string symbolFile,
    Dictionary<string, DocumentState> documents,
    out string? sourceText) {
    foreach ((string uri, DocumentState state) in documents) {
        string openFilePath = UriToFilePath(uri);
        if (PathsMatch(openFilePath, symbolFile)) {
            sourceText = state.Text;
            return true;
        }
    }

    if (Path.IsPathRooted(symbolFile) && File.Exists(symbolFile)) {
        sourceText = File.ReadAllText(symbolFile);
        return true;
    }

    if (TryReadBuiltinLibrary(symbolFile, out sourceText)) {
        return true;
    }

    sourceText = null;
    return false;
}

static bool TryReadBuiltinLibrary(string symbolFile, out string? sourceText) {
    sourceText = null;
    string fileName = Path.GetFileName(symbolFile);
    if (fileName is not ("std.nip" or "ppu.nip" or "hardware.nip")) {
        return false;
    }

    Assembly compilerAssembly = typeof(Preprocesser).Assembly;
    using Stream? stream = compilerAssembly.GetManifestResourceStream($"Catnip.Compiler.Libraries.{fileName}");
    if (stream == null) {
        return false;
    }

    using StreamReader reader = new(stream);
    sourceText = reader.ReadToEnd();
    return true;
}

static object HandleOnTypeFormatting(JsonElement parameters, Dictionary<string, DocumentState> documents) {
    string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString() ?? string.Empty;
    if (string.IsNullOrEmpty(uri) || !documents.TryGetValue(uri, out DocumentState? state)) {
        return Array.Empty<object>();
    }

    JsonElement position = parameters.GetProperty("position");
    int line = position.GetProperty("line").GetInt32();
    int character = position.GetProperty("character").GetInt32();
    string trigger = parameters.TryGetProperty("ch", out JsonElement chElement)
        ? chElement.GetString() ?? string.Empty
        : string.Empty;

    string[] lines = state.Text.Split('\n');
    if (line < 0 || line >= lines.Length) {
        return Array.Empty<object>();
    }

    (string indentUnit, _) = GetIndentOptions(parameters);

    if (trigger == "{") {
        object? edit = BuildOpenBraceEdit(lines, line, character, indentUnit);
        return edit == null ? Array.Empty<object>() : new[] { edit };
    }

    if (trigger == "}") {
        object? edit = BuildCloseBraceIndentEdit(lines, line, indentUnit);
        return edit == null ? Array.Empty<object>() : new[] { edit };
    }

    return Array.Empty<object>();
}

static object? BuildOpenBraceEdit(string[] lines, int line, int character, string indentUnit) {
    string lineText = lines[line];
    int braceIndex = character - 1;
    if (braceIndex < 0 || braceIndex >= lineText.Length || lineText[braceIndex] != '{') {
        return null;
    }

    string trailing = lineText[(braceIndex + 1)..];
    if (!string.IsNullOrWhiteSpace(trailing)) {
        return null;
    }

    if (line + 1 < lines.Length && lines[line + 1].TrimStart().StartsWith("}", StringComparison.Ordinal)) {
        return null;
    }

    string baseIndent = GetLeadingWhitespace(lineText);
    string newText = "\n" + baseIndent + indentUnit + "\n" + baseIndent + "}";

    return new {
        range = new {
            start = new { line, character },
            end = new { line, character }
        },
        newText
    };
}

static object? BuildCloseBraceIndentEdit(string[] lines, int line, string indentUnit) {
    string lineText = lines[line];
    string trimmed = lineText.TrimStart();
    if (!trimmed.StartsWith("}", StringComparison.Ordinal)) {
        return null;
    }

    int currentIndentLength = lineText.Length - trimmed.Length;
    int depth = 0;
    for (int i = 0; i < line; i++) {
        foreach (char c in lines[i]) {
            if (c == '{') depth++;
            else if (c == '}') depth = Math.Max(0, depth - 1);
        }
    }

    int expectedDepth = Math.Max(0, depth - 1);
    string expectedIndent = string.Concat(Enumerable.Repeat(indentUnit, expectedDepth));
    string currentIndent = lineText[..currentIndentLength];
    if (string.Equals(currentIndent, expectedIndent, StringComparison.Ordinal)) {
        return null;
    }

    return new {
        range = new {
            start = new { line, character = 0 },
            end = new { line, character = currentIndentLength }
        },
        newText = expectedIndent
    };
}

static (string indentUnit, bool insertSpaces) GetIndentOptions(JsonElement parameters) {
    bool insertSpaces = true;
    int tabSize = 4;
    if (parameters.TryGetProperty("options", out JsonElement options)) {
        if (options.TryGetProperty("insertSpaces", out JsonElement insertSpacesElement) &&
            insertSpacesElement.ValueKind is JsonValueKind.True or JsonValueKind.False) {
            insertSpaces = insertSpacesElement.GetBoolean();
        }

        if (options.TryGetProperty("tabSize", out JsonElement tabSizeElement) &&
            tabSizeElement.ValueKind == JsonValueKind.Number) {
            tabSize = Math.Max(1, tabSizeElement.GetInt32());
        }
    }

    return (insertSpaces ? new string(' ', tabSize) : "\t", insertSpaces);
}

static string GetLeadingWhitespace(string line) {
    int i = 0;
    while (i < line.Length && char.IsWhiteSpace(line[i])) {
        i++;
    }
    return line[..i];
}

static object HandleSemanticTokens(
    JsonElement parameters,
    Dictionary<string, DocumentState> documents,
    string[] legend) {
    string uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString() ?? string.Empty;
    if (string.IsNullOrEmpty(uri) || !documents.TryGetValue(uri, out DocumentState? state)) {
        return new { data = Array.Empty<int>() };
    }

    List<SemanticToken> absoluteTokens = LexSemanticTokens(state.Text, legend);
    absoluteTokens.Sort((a, b) => {
        int lineCmp = a.Line.CompareTo(b.Line);
        return lineCmp != 0 ? lineCmp : a.Start.CompareTo(b.Start);
    });

    List<int> data = [];
    int previousLine = 0;
    int previousStart = 0;
    foreach (SemanticToken token in absoluteTokens) {
        int deltaLine = token.Line - previousLine;
        int deltaStart = deltaLine == 0 ? token.Start - previousStart : token.Start;
        data.Add(deltaLine);
        data.Add(deltaStart);
        data.Add(token.Length);
        data.Add(token.TokenType);
        data.Add(0);
        previousLine = token.Line;
        previousStart = token.Start;
    }

    return new { data = data.ToArray() };
}

static List<SemanticToken> LexSemanticTokens(string text, string[] legend) {
    Dictionary<string, int> tokenType = legend
        .Select((name, index) => (name, index))
        .ToDictionary(v => v.name, v => v.index, StringComparer.Ordinal);

    HashSet<string> keywords = [
        "fun", "struct", "global", "let", "return", "if", "else", "while", "switch", "case", "default"
    ];

    List<SemanticToken> result = [];
    string[] lines = text.Split('\n');
    for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++) {
        string line = lines[lineIndex];
        int i = 0;
        while (i < line.Length) {
            char c = line[i];
            if (c == '/' && i + 1 < line.Length && line[i + 1] == '/') {
                result.Add(new SemanticToken(lineIndex, i, line.Length - i, tokenType["comment"]));
                break;
            }
            if (c == '"') {
                int start = i;
                i++;
                while (i < line.Length) {
                    if (line[i] == '\\' && i + 1 < line.Length) { i += 2; continue; }
                    if (line[i] == '"') { i++; break; }
                    i++;
                }
                result.Add(new SemanticToken(lineIndex, start, i - start, tokenType["string"]));
                continue;
            }
            if (char.IsDigit(c)) {
                int start = i;
                i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == 'x')) i++;
                result.Add(new SemanticToken(lineIndex, start, i - start, tokenType["number"]));
                continue;
            }
            if (char.IsLetter(c) || c is '_' or '$') {
                int start = i;
                i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] is '_' or '#')) i++;
                string identifier = line[start..i];
                int type = tokenType["variable"];
                if (keywords.Contains(identifier)) type = tokenType["keyword"];
                else if (identifier.StartsWith('$') || identifier.Contains('#') || char.IsUpper(identifier[0])) type = tokenType["struct"];
                else if (i < line.Length && line[i] == '(') type = tokenType["function"];
                result.Add(new SemanticToken(lineIndex, start, i - start, type));
                continue;
            }
            if ("+-*/%=&|!<>:^~".Contains(c)) {
                result.Add(new SemanticToken(lineIndex, i, 1, tokenType["operator"]));
                i++;
                continue;
            }
            i++;
        }
    }

    return result;
}

static IEnumerable<CatnipSymbolDefinition> GetEffectiveSymbols(DocumentState state, string filePath) {
    List<CatnipSymbolDefinition> symbols = [];
    if (state.Result.SymbolIndex != null) {
        symbols.AddRange(state.Result.SymbolIndex.Definitions);
    }
    symbols.AddRange(ExtractImportedSymbols(state.Text, filePath));
    symbols.AddRange(ExtractTextSymbols(state.Text, filePath));
    return symbols
        .GroupBy(d => (d.Name, d.Kind, d.File, d.Line, d.Column))
        .Select(group => group
            .OrderByDescending(def => def.Detail?.Contains("Documentation:", StringComparison.Ordinal) == true)
            .ThenByDescending(def => def.Detail?.Length ?? 0)
            .First());
}

static IEnumerable<CatnipSymbolDefinition> ExtractImportedSymbols(string text, string filePath) {
    try {
        string fullPath = Path.GetFullPath(filePath);
        Preprocesser preprocesser = new(fullPath, text);
        PreprocessedResult preprocessed = preprocesser.Process();

        Dictionary<string, List<string>> linesByFile = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < preprocessed.Lines.Length; i++) {
            string mappedFile = preprocessed.LineMappings[i].File;
            if (PathsMatch(mappedFile, fullPath)) {
                continue;
            }

            if (!linesByFile.TryGetValue(mappedFile, out List<string>? fileLines)) {
                fileLines = [];
                linesByFile[mappedFile] = fileLines;
            }

            fileLines.Add(preprocessed.Lines[i]);
        }

        List<CatnipSymbolDefinition> symbols = [];
        foreach ((string importedFile, List<string> importedLines) in linesByFile) {
            string importedText = string.Join('\n', importedLines);
            symbols.AddRange(ExtractTextSymbols(importedText, importedFile));
        }

        return symbols;
    }
    catch {
        return [];
    }
}

static IEnumerable<CatnipSymbolDefinition> ExtractTextSymbols(string text, string filePath) {
    List<CatnipSymbolDefinition> symbols = [];
    string[] lines = text.Split('\n');
    Regex functionPattern = new(@"^\s*fun\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(([^)]*)\)", RegexOptions.Compiled);
    Regex structPattern = new(@"^\s*struct\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
    Regex globalPattern = new(@"^\s*global\s+([A-Za-z_][A-Za-z0-9_]*)\s*:", RegexOptions.Compiled);
    Regex localPattern = new(@"^\s*let\s+([A-Za-z_][A-Za-z0-9_]*)\s*:", RegexOptions.Compiled);
    Regex docPattern = new(@"^\s*///\s?(.*)$", RegexOptions.Compiled);
    List<string> pendingDocLines = [];

    for (int line = 0; line < lines.Length; line++) {
        string lineText = lines[line];
        Match docMatch = docPattern.Match(lineText);
        if (docMatch.Success) {
            pendingDocLines.Add(docMatch.Groups[1].Value);
            continue;
        }

        string? documentation = pendingDocLines.Count == 0
            ? null
            : string.Join('\n', pendingDocLines).Trim();

        Match functionMatch = functionPattern.Match(lineText);
        if (functionMatch.Success) {
            Group nameGroup = functionMatch.Groups[1];
            symbols.Add(new CatnipSymbolDefinition(
                nameGroup.Value,
                CatnipSymbolKind.Function,
                filePath,
                line,
                nameGroup.Index,
                nameGroup.Index + nameGroup.Length,
                null,
                BuildDetail($"fun {nameGroup.Value}", documentation)));

            foreach (Match parameterMatch in Regex.Matches(functionMatch.Groups[2].Value, @"([A-Za-z_][A-Za-z0-9_]*)\s*:")) {
                int parameterColumn = functionMatch.Groups[2].Index + parameterMatch.Groups[1].Index;
                symbols.Add(new CatnipSymbolDefinition(
                    parameterMatch.Groups[1].Value,
                    CatnipSymbolKind.Parameter,
                    filePath,
                    line,
                    parameterColumn,
                    parameterColumn + parameterMatch.Groups[1].Length,
                    nameGroup.Value,
                    parameterMatch.Groups[1].Value));
            }
        }

        Match structMatch = structPattern.Match(lineText);
        if (structMatch.Success) {
            Group nameGroup = structMatch.Groups[1];
            symbols.Add(new CatnipSymbolDefinition(
                nameGroup.Value,
                CatnipSymbolKind.Struct,
                filePath,
                line,
                nameGroup.Index,
                nameGroup.Index + nameGroup.Length,
                null,
                BuildDetail($"struct {nameGroup.Value}", documentation)));
        }

        Match globalMatch = globalPattern.Match(lineText);
        if (globalMatch.Success) {
            Group nameGroup = globalMatch.Groups[1];
            symbols.Add(new CatnipSymbolDefinition(
                nameGroup.Value,
                CatnipSymbolKind.Global,
                filePath,
                line,
                nameGroup.Index,
                nameGroup.Index + nameGroup.Length,
                null,
                BuildDetail($"global {nameGroup.Value}", documentation)));
        }

        Match localMatch = localPattern.Match(lineText);
        if (localMatch.Success) {
            Group nameGroup = localMatch.Groups[1];
            symbols.Add(new CatnipSymbolDefinition(
                nameGroup.Value,
                CatnipSymbolKind.Local,
                filePath,
                line,
                nameGroup.Index,
                nameGroup.Index + nameGroup.Length,
                null,
                BuildDetail($"let {nameGroup.Value}", documentation)));
        }

        pendingDocLines.Clear();
    }

    return symbols;

    static string BuildDetail(string declaration, string? documentation) {
        if (string.IsNullOrWhiteSpace(documentation)) {
            return declaration;
        }

        return declaration + "\n\nDocumentation:\n" + documentation;
    }
}

static CatnipSymbolDefinition? FindDefinition(
    IEnumerable<CatnipSymbolDefinition> symbols,
    string token,
    string currentFile,
    int currentLine) {
    IEnumerable<CatnipSymbolDefinition> candidates = symbols;
    if (token.StartsWith('$')) {
        token = token[1..];
    }

    List<CatnipSymbolDefinition> matching = candidates.Where(s => s.Name == token).ToList();
    if (matching.Count == 0 && token.Contains('#')) {
        string structName = token.Split('#', 2)[0];
        matching = candidates.Where(s => s.Name == token || s.Name == structName).ToList();
    }
    if (matching.Count == 0) return null;

    return matching
        .OrderBy(s => PathsMatch(s.File, currentFile) ? 0 : 1)
        .ThenBy(s => PathsMatch(s.File, currentFile) && s.Line <= currentLine ? 0 : 1)
        .ThenByDescending(s => PathsMatch(s.File, currentFile) ? s.Line : -1)
        .ThenByDescending(s => s.Detail?.Contains("Documentation:", StringComparison.Ordinal) == true)
        .ThenByDescending(s => s.Detail?.Length ?? 0)
        .ThenBy(s => s.Kind switch {
            CatnipSymbolKind.Local => 0,
            CatnipSymbolKind.Parameter => 1,
            CatnipSymbolKind.Global => 2,
            CatnipSymbolKind.Function => 3,
            CatnipSymbolKind.Struct => 4,
            CatnipSymbolKind.StructField => 5,
            _ => 6
        })
        .First();
}

static int ToLspSymbolKind(CatnipSymbolKind symbolKind) {
    return symbolKind switch {
        CatnipSymbolKind.Function => 12,
        CatnipSymbolKind.Struct => 23,
        CatnipSymbolKind.StructField => 8,
        CatnipSymbolKind.Global => 13,
        CatnipSymbolKind.Local => 13,
        CatnipSymbolKind.Parameter => 13,
        CatnipSymbolKind.BinaryGlobal => 14,
        _ => 13
    };
}

static FrontendCompilationResult Analyse(CatnipFrontendService frontendService, string filePath, string text) {
    string fullPath = Path.GetFullPath(filePath);
    string workingDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
    FrontendCompilationResult primary = frontendService.AnalyseSource(fullPath, text, workingDirectory);
    if (!ShouldRetryWithMacroPlaceholderFallback(primary, text)) {
        return primary;
    }

    string fallbackText = ReplaceMacroPlaceholdersWithZero(text);
    FrontendCompilationResult fallback = frontendService.AnalyseSource(fullPath, fallbackText, workingDirectory);
    bool useFallback = CountParseDiagnostics(fallback) < CountParseDiagnostics(primary);
    return useFallback ? fallback : primary;
}

static bool ShouldRetryWithMacroPlaceholderFallback(FrontendCompilationResult result, string text) {
    return text.Contains("${", StringComparison.Ordinal) && CountParseDiagnostics(result) > 0;
}

static int CountParseDiagnostics(FrontendCompilationResult result) {
    return result.Diagnostics.Count(d =>
        d.Message.Contains("Failed to parse code:", StringComparison.Ordinal));
}

static string ReplaceMacroPlaceholdersWithZero(string text) {
    return Regex.Replace(text, @"\$\{[A-Za-z_][A-Za-z0-9_]*\}", "0");
}

static bool VerboseStderrLoggingEnabled() {
    string? value = Environment.GetEnvironmentVariable("CATNIP_LSP_STDERR_VERBOSE");
    return value is "1" or "true" or "TRUE" or "yes" or "YES" or "on" or "ON";
}

static void LogStderr(string message) {
    Console.Error.WriteLine($"[catnip-lsp:{DateTime.UtcNow:O}] {message}");
}

static HashSet<string> PublishDiagnosticsForDocument(
    Stream output,
    string mainUri,
    IReadOnlyList<CatnipDiagnostic> diagnostics,
    IReadOnlyCollection<string> previousUris) {
    string mainFilePath = UriToFilePath(mainUri);
    Dictionary<string, List<CatnipDiagnostic>> grouped = new(StringComparer.OrdinalIgnoreCase);

    foreach (CatnipDiagnostic diagnostic in diagnostics) {
        string targetUri = ResolveDiagnosticUri(mainUri, mainFilePath, diagnostic.File);
        if (!grouped.TryGetValue(targetUri, out List<CatnipDiagnostic>? list)) {
            list = [];
            grouped[targetUri] = list;
        }
        list.Add(diagnostic);
    }

    foreach ((string targetUri, List<CatnipDiagnostic> groupedDiagnostics) in grouped) {
        PublishDiagnostics(output, targetUri, groupedDiagnostics);
    }

    HashSet<string> newUris = grouped.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (string staleUri in previousUris) {
        if (!newUris.Contains(staleUri)) {
            PublishDiagnostics(output, staleUri, Array.Empty<CatnipDiagnostic>());
        }
    }

    if (diagnostics.Count == 0) {
        PublishDiagnostics(output, mainUri, Array.Empty<CatnipDiagnostic>());
        newUris.Add(mainUri);
    }

    return newUris;
}

static string ResolveDiagnosticUri(string mainUri, string mainFilePath, string diagnosticFile) {
    if (string.IsNullOrWhiteSpace(diagnosticFile) || diagnosticFile == "unknown") return mainUri;
    if (Uri.TryCreate(diagnosticFile, UriKind.Absolute, out Uri? absoluteUri) && absoluteUri.IsFile) {
        return absoluteUri.ToString();
    }
    if (Path.IsPathRooted(diagnosticFile)) {
        return FilePathToUri(diagnosticFile);
    }

    string mainDirectory = Path.GetDirectoryName(mainFilePath) ?? Directory.GetCurrentDirectory();
    string candidate = Path.GetFullPath(Path.Combine(mainDirectory, diagnosticFile));
    if (File.Exists(candidate)) {
        return FilePathToUri(candidate);
    }

    if (string.Equals(Path.GetFileName(mainFilePath), diagnosticFile, StringComparison.OrdinalIgnoreCase)) {
        return mainUri;
    }

    return mainUri;
}

static bool PathsMatch(string left, string right) {
    try {
        return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
    }
    catch {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}

static string ApplyRangeChange(string text, JsonElement range, string replacementText) {
    JsonElement start = range.GetProperty("start");
    JsonElement end = range.GetProperty("end");
    int startOffset = PositionToOffset(text, start.GetProperty("line").GetInt32(), start.GetProperty("character").GetInt32());
    int endOffset = PositionToOffset(text, end.GetProperty("line").GetInt32(), end.GetProperty("character").GetInt32());
    if (startOffset > endOffset) (startOffset, endOffset) = (endOffset, startOffset);
    return text[..startOffset] + replacementText + text[endOffset..];
}

static int PositionToOffset(string text, int line, int character) {
    int currentLine = 0;
    int offset = 0;

    while (currentLine < line && offset < text.Length) {
        int newlineIndex = text.IndexOf('\n', offset);
        if (newlineIndex < 0) return text.Length;
        offset = newlineIndex + 1;
        currentLine++;
    }

    int lineEnd = text.IndexOf('\n', offset);
    if (lineEnd < 0) lineEnd = text.Length;
    return Math.Clamp(offset + character, offset, lineEnd);
}

static string UriToFilePath(string uriString) {
    Uri uri = new(uriString);
    return uri.IsFile ? uri.LocalPath : uriString;
}

static string FilePathToUri(string filePath) {
    if (!Path.IsPathRooted(filePath)) return filePath;
    return new Uri(Path.GetFullPath(filePath)).ToString();
}

static void PublishDiagnostics(Stream output, string uri, IReadOnlyList<CatnipDiagnostic> diagnostics) {
    object payload = new {
        jsonrpc = "2.0",
        method = "textDocument/publishDiagnostics",
        @params = new {
            uri,
            diagnostics = diagnostics.Select(d => new {
                range = new {
                    start = new { line = d.Range.StartLine, character = d.Range.StartColumn },
                    end = new { line = d.Range.EndLine, character = d.Range.EndColumn }
                },
                severity = (int)d.Severity,
                code = d.Code,
                source = "catnip",
                message = d.Message
            }).ToArray()
        }
    };

    WriteJsonMessage(output, payload);
}

static string GetTokenAt(string text, int line, int character) {
    string[] lines = text.Split('\n');
    if (line < 0 || line >= lines.Length) return string.Empty;
    string lineText = lines[line];
    if (lineText.Length == 0) return string.Empty;

    int index = Math.Clamp(character, 0, lineText.Length - 1);
    if (!IsTokenCharacter(lineText[index]) && index > 0 && IsTokenCharacter(lineText[index - 1])) index--;
    if (!IsTokenCharacter(lineText[index])) return string.Empty;

    int start = index;
    while (start > 0 && IsTokenCharacter(lineText[start - 1])) start--;
    int end = index;
    while (end + 1 < lineText.Length && IsTokenCharacter(lineText[end + 1])) end++;
    return lineText[start..(end + 1)];
}

static bool IsTokenCharacter(char c) {
    return char.IsLetterOrDigit(c) || c is '_' or '#' or '$';
}

static bool TryReadMessage(Stream input, out string? payload) {
    payload = null;
    int contentLength = -1;

    List<byte> headerBytes = [];
    while (true) {
        int value = input.ReadByte();
        if (value < 0) return false;
        headerBytes.Add((byte)value);

        int count = headerBytes.Count;
        bool hasCrlfTerminator = count >= 4 &&
            headerBytes[count - 4] == '\r' &&
            headerBytes[count - 3] == '\n' &&
            headerBytes[count - 2] == '\r' &&
            headerBytes[count - 1] == '\n';
        bool hasLfTerminator = count >= 2 &&
            headerBytes[count - 2] == '\n' &&
            headerBytes[count - 1] == '\n';
        if (hasCrlfTerminator || hasLfTerminator) {
            break;
        }
    }

    string headers = Encoding.ASCII.GetString(headerBytes.ToArray())
        .Replace("\r\n", "\n", StringComparison.Ordinal);
    foreach (string headerLine in headers.Split('\n', StringSplitOptions.RemoveEmptyEntries)) {
        const string contentLengthPrefix = "Content-Length:";
        if (!headerLine.StartsWith(contentLengthPrefix, StringComparison.OrdinalIgnoreCase)) continue;
        if (int.TryParse(headerLine[contentLengthPrefix.Length..].Trim(), out int parsedLength)) {
            contentLength = parsedLength;
            break;
        }
    }

    if (contentLength < 0) return false;

    byte[] buffer = new byte[contentLength];
    int read = 0;
    while (read < contentLength) {
        int chunk = input.Read(buffer, read, contentLength - read);
        if (chunk <= 0) return false;
        read += chunk;
    }

    payload = Encoding.UTF8.GetString(buffer);
    return true;
}

static void WriteResponse(Stream output, JsonElement id, object? result) {
    JsonObject response = new() {
        ["jsonrpc"] = "2.0",
        ["id"] = JsonNode.Parse(id.GetRawText()),
        ["result"] = JsonSerializer.SerializeToNode(result)
    };
    WriteJsonMessage(output, response);
}

static void WriteErrorResponse(Stream output, JsonElement id, int code, string message) {
    JsonObject response = new() {
        ["jsonrpc"] = "2.0",
        ["id"] = JsonNode.Parse(id.GetRawText()),
        ["error"] = new JsonObject {
            ["code"] = code,
            ["message"] = message
        }
    };
    WriteJsonMessage(output, response);
}

static void WriteJsonMessage(Stream output, object payload) {
    byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload);
    byte[] header = Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n");
    output.Write(header, 0, header.Length);
    output.Write(body, 0, body.Length);
    output.Flush();
}

file sealed record DocumentState(
    string Text,
    FrontendCompilationResult Result,
    HashSet<string> PublishedDiagnosticUris);

file sealed record SemanticToken(int Line, int Start, int Length, int TokenType);
