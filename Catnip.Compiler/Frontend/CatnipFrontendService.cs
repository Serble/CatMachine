using Catnip.Compiler.Analysis;
using Catnip.Compiler.Ast;
using Catnip.Compiler.Parser;

namespace Catnip.Compiler.Frontend;

public sealed class CatnipFrontendService {
    public FrontendCompilationResult AnalyseFile(string inputPath) {
        string fullPath = Path.GetFullPath(inputPath);
        string workingDirectory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        return AnalyseSource(fullPath, File.ReadAllText(fullPath), workingDirectory);
    }

    public FrontendCompilationResult AnalyseSource(string mainFileName, string source, string workingDirectory) {
        return AnalyseSource(mainFileName, source, workingDirectory, null);
    }

    public FrontendCompilationResult AnalyseSource(
        string mainFileName,
        string source,
        string workingDirectory,
        IReadOnlyDictionary<string, string>? virtualFiles) {
        string originalWorkingDirectory = Directory.GetCurrentDirectory();
        try {
            Directory.SetCurrentDirectory(workingDirectory);

            IReadOnlyDictionary<string, string> normalizedVirtualFiles = virtualFiles == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : virtualFiles.ToDictionary(
                    kvp => Path.GetFullPath(kvp.Key),
                    kvp => kvp.Value,
                    StringComparer.OrdinalIgnoreCase);

            Preprocesser preprocesser = new(mainFileName, source, normalizedVirtualFiles);
            PreprocessedResult preprocessedResult = preprocesser.Process();
            if (preprocessedResult.Lines.Length != preprocessedResult.LineMappings.Length) {
                throw new InvalidOperationException("Preprocessor returned mismatched line and mapping counts");
            }

            ParsedElement[] parsedElements = CodeParser.ParseCode(
                string.Join('\n', preprocessedResult.Lines),
                preprocessedResult.LineMappings);
            CatnipSymbolIndex fallbackSymbolIndex = CatnipSymbolIndex.BuildFromParsedElements(
                parsedElements,
                preprocessedResult.BinaryGlobals);

            try {
                Analyser analyser = new(parsedElements, preprocessedResult.BinaryGlobals, preprocessedResult.VisibleFilesByFile);
                CatProgram program = analyser.Analyse();
                CatnipSymbolIndex symbolIndex = CatnipSymbolIndex.Build(program);
                return new FrontendCompilationResult(mainFileName, program, [], symbolIndex);
            }
            catch (AggregateException aggregateException) {
                List<CatnipDiagnostic> diagnostics = [];
                foreach (Exception exception in aggregateException.Flatten().InnerExceptions) {
                    if (exception is CompilationFailureException cfe) {
                        diagnostics.Add(ToDiagnostic(cfe));
                    }
                }

                if (diagnostics.Count == 0) {
                    diagnostics.Add(new CatnipDiagnostic(
                        mainFileName,
                        new CatnipSourceRange(0, 0, 0, 1),
                        aggregateException.Message,
                        CatnipDiagnosticSeverity.Error));
                }

                return new FrontendCompilationResult(mainFileName, null, diagnostics, fallbackSymbolIndex);
            }
        }
        catch (CompilationFailureException cfe) {
            return new FrontendCompilationResult(mainFileName, null, [ToDiagnostic(cfe)], null);
        }
        catch (Exception exception) {
            return new FrontendCompilationResult(mainFileName, null, [
                new CatnipDiagnostic(
                    mainFileName,
                    new CatnipSourceRange(0, 0, 0, 1),
                    exception.Message,
                    CatnipDiagnosticSeverity.Error)
            ], null);
        }
        finally {
            Directory.SetCurrentDirectory(originalWorkingDirectory);
        }
    }

    private static CatnipDiagnostic ToDiagnostic(CompilationFailureException exception) {
        (string file, int line, int column) = ParseLocationFromMessage(exception.Message);

        int normalizedLine = Math.Max(0, line - 1);
        int normalizedColumn = Math.Max(0, column > 0 ? column - 1 : 0);
        CatnipSourceRange range = new(normalizedLine, normalizedColumn, normalizedLine, normalizedColumn + 1);

        return new CatnipDiagnostic(
            file,
            range,
            exception.Message,
            CatnipDiagnosticSeverity.Error,
            "CATNIP",
            exception.Context);
    }

    private static (string File, int Line, int Column) ParseLocationFromMessage(string message) {
        // Message shape: [file:line:column] text
        if (!message.StartsWith('[')) {
            return ("unknown", 0, 0);
        }

        int end = message.IndexOf(']');
        if (end <= 1) return ("unknown", 0, 0);

        string location = message[1..end];
        string[] parts = location.Split(':');
        if (parts.Length < 3) return ("unknown", 0, 0);

        string file = string.Join(':', parts.Take(parts.Length - 2));
        bool lineValid = int.TryParse(parts[^2], out int line);
        bool columnValid = int.TryParse(parts[^1], out int column);

        return (
            file,
            lineValid ? line : 0,
            columnValid ? column : 0
        );
    }
}
