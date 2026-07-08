using Catnip.Compiler.Ast;

namespace Catnip.Compiler.Frontend;

public sealed record FrontendCompilationResult(
    string MainFile,
    CatProgram? Program,
    IReadOnlyList<CatnipDiagnostic> Diagnostics,
    CatnipSymbolIndex? SymbolIndex) {
    public bool Succeeded => Diagnostics.Count == 0 && Program != null;
}
