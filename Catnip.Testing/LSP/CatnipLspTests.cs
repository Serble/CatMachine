using System.Text.Json;

namespace Catnip.Testing.LSP;

public class CatnipLspTests {
    [Test]
    public void Initialize_ReturnsCoreCapabilities() {
        using LspTestClient client = new();

        JsonElement result = client.Initialize();
        JsonElement capabilities = result.GetProperty("capabilities");

        Assert.Multiple(() => {
            Assert.That(capabilities.GetProperty("definitionProvider").GetBoolean(), Is.True);
            Assert.That(capabilities.GetProperty("hoverProvider").GetBoolean(), Is.True);
            Assert.That(capabilities.GetProperty("documentSymbolProvider").GetBoolean(), Is.True);
            Assert.That(capabilities.TryGetProperty("documentOnTypeFormattingProvider", out _), Is.True);
        });
    }

    [Test]
    public void DidOpen_InvalidCode_PublishesDiagnostics() {
        using LspTestClient client = new();
        client.Initialize();

        string source = """
                        fun main(port:4) {
                            let asd:3 = port:3;
                        }
                        """;

        string file = Path.Combine(Path.GetTempPath(), $"catnip-invalid-{Guid.NewGuid():N}.nip");
        File.WriteAllText(file, source);
        try {
            string uri = new Uri(file).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });

            JsonElement publish = client.WaitForNotification("textDocument/publishDiagnostics");
            JsonElement diagnostics = publish.GetProperty("params").GetProperty("diagnostics");
            Assert.That(diagnostics.GetArrayLength(), Is.GreaterThan(0));
            Assert.That(
                diagnostics.EnumerateArray().Any(d =>
                    d.GetProperty("message").GetString()!.Contains("mov-compatible", StringComparison.OrdinalIgnoreCase)),
                Is.True);
        }
        finally {
            File.Delete(file);
        }
    }

    [Test]
    public void Completion_IncludesDocumentSymbols() {
        using LspTestClient client = new();
        client.Initialize();

        string source = """
                        global hp:4 = 10;
                        fun main() {
                            let local_value:4 = hp:4;
                            return local_value:4;
                        }
                        """;

        string file = Path.Combine(Path.GetTempPath(), $"catnip-completion-{Guid.NewGuid():N}.nip");
        File.WriteAllText(file, source);
        try {
            string uri = new Uri(file).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });
            client.WaitForNotification("textDocument/publishDiagnostics");

            JsonElement completion = client.SendRequest("textDocument/completion", new {
                textDocument = new { uri },
                position = new { line = 2, character = 20 }
            });

            JsonElement items = completion.GetProperty("items");
            List<string> labels = items.EnumerateArray()
                .Select(i => i.GetProperty("label").GetString()!)
                .ToList();

            Assert.That(labels, Does.Contain("hp"));
            Assert.That(labels, Does.Contain("local_value"));
        }
        finally {
            File.Delete(file);
        }
    }

    [Test]
    public void Completion_IncludesBuiltinImportedLibrarySymbols() {
        using LspTestClient client = new();
        client.Initialize();

        string source = """
                        #include "hardware"

                        fun main() {
                            let dev:4 = find_device_port(1);
                            return dev:4;
                        }
                        """;

        string file = Path.Combine(Path.GetTempPath(), $"catnip-completion-builtin-import-{Guid.NewGuid():N}.nip");
        File.WriteAllText(file, source);
        try {
            string uri = new Uri(file).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });
            client.WaitForNotification("textDocument/publishDiagnostics");

            JsonElement completion = client.SendRequest("textDocument/completion", new {
                textDocument = new { uri },
                position = new { line = 3, character = 22 }
            });

            JsonElement items = completion.GetProperty("items");
            List<string> labels = items.EnumerateArray()
                .Select(i => i.GetProperty("label").GetString()!)
                .ToList();

            Assert.That(labels, Does.Contain("find_device_port"));
            Assert.That(labels, Does.Contain("list_devices"));
        }
        finally {
            File.Delete(file);
        }
    }

    [Test]
    public void Completion_IncludesUserImportedLibrarySymbols() {
        using LspTestClient client = new();
        client.Initialize();

        string tempDir = Path.Combine(Path.GetTempPath(), $"catnip-completion-user-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string mainPath = Path.Combine(tempDir, "main.nip");
        string libPath = Path.Combine(tempDir, "helpers.nip");
        string source = """
                        #include "helpers.nip"

                        fun main() {
                            helper_fn();
                            return;
                        }
                        """;
        File.WriteAllText(mainPath, source);
        File.WriteAllText(libPath, """
                                  fun helper_fn() {
                                      return;
                                  }
                                  """);
        try {
            string uri = new Uri(mainPath).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });
            client.WaitForNotification("textDocument/publishDiagnostics");

            JsonElement completion = client.SendRequest("textDocument/completion", new {
                textDocument = new { uri },
                position = new { line = 3, character = 8 }
            });

            JsonElement items = completion.GetProperty("items");
            List<string> labels = items.EnumerateArray()
                .Select(i => i.GetProperty("label").GetString()!)
                .ToList();

            Assert.That(labels, Does.Contain("helper_fn"));
        }
        finally {
            Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public void Definition_ResolvesGlobalReference() {
        using LspTestClient client = new();
        client.Initialize();

        string source = """
                        global hp:4 = 10;
                        fun main() {
                            let x:4 = hp:4;
                            return x:4;
                        }
                        """;

        string file = Path.Combine(Path.GetTempPath(), $"catnip-definition-{Guid.NewGuid():N}.nip");
        File.WriteAllText(file, source);
        try {
            string uri = new Uri(file).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });
            client.WaitForNotification("textDocument/publishDiagnostics");

            string usageLine = source.Split('\n')[2];
            int hpChar = usageLine.IndexOf("hp:4", StringComparison.Ordinal);
            Assert.That(hpChar, Is.GreaterThanOrEqualTo(0));

            JsonElement definition = client.SendRequest("textDocument/definition", new {
                textDocument = new { uri },
                position = new { line = 2, character = hpChar } // hp usage
            });

            JsonElement first = definition.EnumerateArray().First();
            Assert.That(first.GetProperty("uri").GetString(), Is.EqualTo(uri));
            JsonElement start = first.GetProperty("range").GetProperty("start");
            Assert.That(start.GetProperty("line").GetInt32(), Is.InRange(0, 2));
        }
        finally {
            File.Delete(file);
        }
    }

    [Test]
    public void Hover_FunctionWithTripleSlashDocs_IncludesDocumentation() {
        using LspTestClient client = new();
        client.Initialize();

        string source = """
                        /// Move the player one tile.
                        fun move_player() {
                            return;
                        }

                        fun main() {
                            move_player();
                            return;
                        }
                        """;

        string file = Path.Combine(Path.GetTempPath(), $"catnip-hover-func-docs-{Guid.NewGuid():N}.nip");
        File.WriteAllText(file, source);
        try {
            string uri = new Uri(file).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });
            client.WaitForNotification("textDocument/publishDiagnostics");

            JsonElement hover = client.SendRequest("textDocument/hover", new {
                textDocument = new { uri },
                position = new { line = 6, character = 6 }
            });

            string value = hover.GetProperty("contents").GetProperty("value").GetString()!;
            Assert.That(value, Does.Contain("Move the player one tile."));
        }
        finally {
            File.Delete(file);
        }
    }

    [Test]
    public void Hover_GlobalVariableWithTripleSlashDocs_IncludesDocumentation() {
        using LspTestClient client = new();
        client.Initialize();

        string source = """
                        /// Current player hit points.
                        global hp:4 = 10;

                        fun main() {
                            let x:4 = hp:4;
                            return x:4;
                        }
                        """;

        string file = Path.Combine(Path.GetTempPath(), $"catnip-hover-var-docs-{Guid.NewGuid():N}.nip");
        File.WriteAllText(file, source);
        try {
            string uri = new Uri(file).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });
            client.WaitForNotification("textDocument/publishDiagnostics");

            JsonElement hover = client.SendRequest("textDocument/hover", new {
                textDocument = new { uri },
                position = new { line = 4, character = 15 }
            });

            string value = hover.GetProperty("contents").GetProperty("value").GetString()!;
            Assert.That(value, Does.Contain("Current player hit points."));
        }
        finally {
            File.Delete(file);
        }
    }

    [Test]
    public void DidOpen_ValidProgramWithStdInclude_ShouldNotReportStdNipParseError_Regression() {
        using LspTestClient client = new();
        client.Initialize();

        string source = """
                        #include "std"

                        main();

                        fun main() {
                            return;
                        }
                        """;

        string file = Path.Combine(Path.GetTempPath(), $"catnip-parse-regression-{Guid.NewGuid():N}.nip");
        File.WriteAllText(file, source);
        try {
            string uri = new Uri(file).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });

            JsonElement publish = client.WaitForNotification("textDocument/publishDiagnostics");
            JsonElement diagnostics = publish.GetProperty("params").GetProperty("diagnostics");

            bool hasRegressionError = diagnostics.EnumerateArray().Any(d =>
                d.GetProperty("message").GetString()!
                    .Contains("Failed to parse code: unexpected 'i', expected ';'",
                        StringComparison.Ordinal));

            Assert.That(hasRegressionError, Is.False);
        }
        finally {
            File.Delete(file);
        }
    }

    [Test]
    public void DidOpen_StdInclude_ShouldResolveSpriteVisibleMacro_Regression() {
        using LspTestClient client = new();
        client.Initialize();

        string source = """
                        #include "std"

                        main();

                        global sprite_attr:1;

                        fun main() {
                            sprite_attr:1 = ${SPRITE_VISIBLE} | 2;
                            return;
                        }
                        """;

        string file = Path.Combine(Path.GetTempPath(), $"catnip-sprite-visible-regression-{Guid.NewGuid():N}.nip");
        File.WriteAllText(file, source);
        try {
            string uri = new Uri(file).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });

            JsonElement publish = client.WaitForNotification("textDocument/publishDiagnostics");
            JsonElement diagnostics = publish.GetProperty("params").GetProperty("diagnostics");

            bool hasMacroParseError = diagnostics.EnumerateArray().Any(d =>
                d.GetProperty("message").GetString()!
                    .Contains("Failed to parse code: unexpected '{', expected letter or _",
                        StringComparison.Ordinal));

            Assert.That(hasMacroParseError, Is.False);
        }
        finally {
            File.Delete(file);
        }
    }

    [Test]
    public void DidOpen_StdInclude_ShouldNotReportNullStreamError_Regression() {
        using LspTestClient client = new();
        client.Initialize();

        string source = """
                        #include "std"

                        main();

                        fun main() {
                            return;
                        }
                        """;

        string file = Path.Combine(Path.GetTempPath(), $"catnip-null-stream-regression-{Guid.NewGuid():N}.nip");
        File.WriteAllText(file, source);
        try {
            string uri = new Uri(file).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });

            JsonElement publish = client.WaitForNotification("textDocument/publishDiagnostics");
            JsonElement diagnostics = publish.GetProperty("params").GetProperty("diagnostics");

            bool hasNullStreamError = diagnostics.EnumerateArray().Any(d =>
                d.GetProperty("message").GetString()!
                    .Contains("Value cannot be null. (Parameter 'stream')",
                        StringComparison.Ordinal));

            Assert.That(hasNullStreamError, Is.False);
        }
        finally {
            File.Delete(file);
        }
    }

    [Test]
    public void DidOpen_PhysicsPointCollides_ShouldNotReportSlashParseError_Regression() {
        using LspTestClient client = new();
        client.Initialize();

        string source = """
                        fun point_collides(x:4, y:4, type:1) {
                            let tilemap_x:2 = x:2 / ${TILE_SIZE};
                            let tilemap_y:2 = y:2 / ${TILE_SIZE};
                            return tilemap_x:2 == tilemap_y:2;
                        }
                        """;

        string file = Path.Combine(Path.GetTempPath(), $"catnip-physics-slash-regression-{Guid.NewGuid():N}.nip");
        File.WriteAllText(file, source);
        try {
            string uri = new Uri(file).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });

            JsonElement publish = client.WaitForNotification("textDocument/publishDiagnostics");
            JsonElement diagnostics = publish.GetProperty("params").GetProperty("diagnostics");

            bool hasSlashParseError = diagnostics.EnumerateArray().Any(d =>
                d.GetProperty("message").GetString()!
                    .Contains("Failed to parse code: unexpected '/', expected ';'",
                        StringComparison.Ordinal));

            Assert.That(hasSlashParseError, Is.False);
        }
        finally {
            File.Delete(file);
        }
    }

    [Test]
    public void OnTypeFormatting_OpenBrace_InsertsClosingBraceAndIndentBlock() {
        using LspTestClient client = new();
        client.Initialize();

        string source = "fun main() {";
        string file = Path.Combine(Path.GetTempPath(), $"catnip-brace-open-{Guid.NewGuid():N}.nip");
        File.WriteAllText(file, source);
        try {
            string uri = new Uri(file).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });

            JsonElement edits = client.SendRequest("textDocument/onTypeFormatting", new {
                textDocument = new { uri },
                position = new { line = 0, character = source.Length },
                ch = "{",
                options = new { tabSize = 4, insertSpaces = true }
            });

            JsonElement first = edits.EnumerateArray().First();
            Assert.That(first.GetProperty("newText").GetString(), Is.EqualTo("\n    \n}"));
        }
        finally {
            File.Delete(file);
        }
    }

    [Test]
    public void OnTypeFormatting_CloseBrace_AlignsClosingBraceIndent() {
        using LspTestClient client = new();
        client.Initialize();

        string source = """
                        fun main() {
                                }
                        """;
        string file = Path.Combine(Path.GetTempPath(), $"catnip-brace-close-{Guid.NewGuid():N}.nip");
        File.WriteAllText(file, source);
        try {
            string uri = new Uri(file).ToString();
            client.SendNotification("textDocument/didOpen", new {
                textDocument = new {
                    uri,
                    languageId = "catnip",
                    version = 1,
                    text = source
                }
            });

            JsonElement edits = client.SendRequest("textDocument/onTypeFormatting", new {
                textDocument = new { uri },
                position = new { line = 1, character = 9 },
                ch = "}",
                options = new { tabSize = 4, insertSpaces = true }
            });

            JsonElement first = edits.EnumerateArray().First();
            JsonElement range = first.GetProperty("range");
            Assert.Multiple(() => {
                Assert.That(first.GetProperty("newText").GetString(), Is.EqualTo(""));
                Assert.That(range.GetProperty("start").GetProperty("line").GetInt32(), Is.EqualTo(1));
                Assert.That(range.GetProperty("start").GetProperty("character").GetInt32(), Is.EqualTo(0));
                Assert.That(range.GetProperty("end").GetProperty("character").GetInt32(), Is.EqualTo(8));
            });
        }
        finally {
            File.Delete(file);
        }
    }
}
