using Catnip.Compiler.Frontend;

namespace Catnip.Testing.Compilation;

public class CatnipFrontendTests {
    private static FrontendCompilationResult AnalyseSource(
        CatnipFrontendService frontend,
        string mainPath,
        string source,
        IReadOnlyDictionary<string, string>? virtualFiles = null) {
        string workingDirectory = Directory.GetCurrentDirectory();
        return frontend.AnalyseSource(mainPath, source, workingDirectory, virtualFiles);
    }

    [Test]
    public void AnalyseSource_ValidCode_HasNoDiagnostics() {
        CatnipFrontendService frontend = new();
        string mainPath = "/virtual/frontend/inline.nip";
        string source = """
                        fun main() {
                            let x:4 = 1;
                            return x:4;
                        }
                        """;

        FrontendCompilationResult result = AnalyseSource(frontend, mainPath, source);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.SymbolIndex, Is.Not.Null);
    }

    [Test]
    public void AnalyseSource_InvalidDereferenceSize_ReportsDiagnostic() {
        CatnipFrontendService frontend = new();
        string mainPath = "/virtual/frontend/invalid-deref.nip";
        string source = """
                        fun main(port:4) {
                            let asd:3 = port:3;
                        }
                        """;

        FrontendCompilationResult result = AnalyseSource(frontend, mainPath, source);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Diagnostics.Any(d => d.Message.Contains("mov-compatible", StringComparison.OrdinalIgnoreCase)),
            Is.True);
    }

    [Test]
    public void AnalyseFile_IncludesAreDeduplicated_AndFollowFirstEncounterOrder() {
        CatnipFrontendService frontend = new();
        string root = "/virtual/frontend/include-order";
        string mainPath = Path.Combine(root, "main.nip");
        string aPath = Path.Combine(root, "a.nip");
        string bPath = Path.Combine(root, "b.nip");
        string cPath = Path.Combine(root, "c.nip");

        Dictionary<string, string> virtualFiles = new(StringComparer.OrdinalIgnoreCase) {
            [aPath] = """
                      #include "c.nip"

                      fun a() {
                          return;
                      }
                      """,
            [bPath] = """
                      #include "c.nip"

                      fun b() {
                          return;
                      }
                      """,
            [cPath] = """
                      fun c() {
                          return;
                      }
                      """
        };
        FrontendCompilationResult result = AnalyseSource(frontend, mainPath, """
                                                                     #include "a.nip"
                                                                     #include "b.nip"

                                                                     fun main() {
                                                                         return;
                                                                     }
                                                                     """, virtualFiles);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Diagnostics, Is.Empty);
        Assert.That(result.Program, Is.Not.Null);

        string[] functionOrder = result.Program!.Functions.Select(f => f.Name).ToArray();
        Assert.That(functionOrder, Is.EqualTo(["main", "a", "c", "b"]));
    }

    [Test]
    public void AnalyseFile_FileCannotUseMainGlobalsWithoutExplicitInclude() {
        CatnipFrontendService frontend = new();
        string root = "/virtual/frontend/visibility-fail";
        string mainPath = Path.Combine(root, "main.nip");
        string physicsPath = Path.Combine(root, "physics.nip");

        Dictionary<string, string> virtualFiles = new(StringComparer.OrdinalIgnoreCase) {
            [physicsPath] = """
                            fun physics_read() {
                                return shared_value:1;
                            }
                            """
        };
        FrontendCompilationResult result = AnalyseSource(frontend, mainPath, """
                                                                      #include "physics.nip"

                                                                      global shared_value:1 = 1;

                                                                      fun main() {
                                                                          return;
                                                                      }
                                                                      """, virtualFiles);
        Assert.That(result.Succeeded, Is.False);
        Assert.That(
            result.Diagnostics.Any(d => d.Message.Contains("not visible", StringComparison.OrdinalIgnoreCase)),
            Is.True);
    }

    [Test]
    public void AnalyseFile_FileCanUseGlobalsFromExplicitlyIncludedFile() {
        CatnipFrontendService frontend = new();
        string root = "/virtual/frontend/visibility-pass";
        string mainPath = Path.Combine(root, "main.nip");
        string commonPath = Path.Combine(root, "common.nip");
        string physicsPath = Path.Combine(root, "physics.nip");

        Dictionary<string, string> virtualFiles = new(StringComparer.OrdinalIgnoreCase) {
            [commonPath] = """
                           global shared_value:1 = 1;
                           """,
            [physicsPath] = """
                            #include "common.nip"

                            fun physics_read() {
                                return shared_value:1;
                            }
                            """
        };
        FrontendCompilationResult result = AnalyseSource(frontend, mainPath, """
                                                                      #include "common.nip"
                                                                      #include "physics.nip"

                                                                      fun main() {
                                                                          return;
                                                                      }
                                                                      """, virtualFiles);
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Diagnostics, Is.Empty);
    }

    [Test]
    public void AnalyseFile_PassingFunctionAsCallback_DoesNotRequireGlobalDeclaration() {
        CatnipFrontendService frontend = new();
        string mainPath = "/virtual/frontend/callback/main.nip";
        FrontendCompilationResult result = AnalyseSource(frontend, mainPath, """
                                                                      fun list_devices(callback:4) {
                                                                          (callback:4)();
                                                                      }

                                                                      fun _find_device_callback() {
                                                                          return;
                                                                      }

                                                                      fun main() {
                                                                          list_devices(_find_device_callback);
                                                                          return;
                                                                      }
                                                                      """);
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Diagnostics, Is.Empty);
    }
}
