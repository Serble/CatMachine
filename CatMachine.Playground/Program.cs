using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CatAssembler.Analysis;
using CatAssembler.Assembler;
using CatAssembler.Exceptions;
using CatAssembler.Parser;
using CatVM;
using HardwareManagerDevice;

namespace CatMachine.Playground;

[SupportedOSPlatform("browser")]
internal static partial class Program {
    private const int MemoryBytes = 1024 * 1024;
    private const int CyclesPerSecond = 1_000_000;
    private const int MaxOutputChars = 64 * 1024;
    private const int MaxWorkspaceBytes = 4 * 1024 * 1024;
    private const int MaxWorkspaceFiles = 256;
    public static void Main() { }

    [JSExport]
    internal static string Run(string entryFile, string filesJson) {
        WorkspaceFiles? workspace = JsonSerializer.Deserialize(
            filesJson,
            PlaygroundJsonContext.Default.WorkspaceFiles
        );
        if (workspace?.Files is not { Count: > 0 }) {
            return Serialize(new RunResult(false, "The workspace does not contain any source files.", 0));
        }

        Dictionary<string, string> files = ValidateWorkspace(workspace.Files);
        string normalizedEntryFile = NormalizeRelativePath(entryFile);
        if (!files.TryGetValue(normalizedEntryFile, out string? source)) {
            return Serialize(new RunResult(false, $"Entry file not found: {normalizedEntryFile}", 0));
        }

        string originalDirectory = Directory.GetCurrentDirectory();
        string workspaceDirectory = Path.Combine(Path.GetTempPath(), $"catmachine-{Guid.NewGuid():N}");
        TextWriter originalOutput = Console.Out;
        try {
            MaterializeWorkspace(workspaceDirectory, files);
            Directory.SetCurrentDirectory(workspaceDirectory);

            using MemoryStream romStream = new();
            Console.SetOut(TextWriter.Null);

            Tokeniser tokeniser = new(normalizedEntryFile, source);
            Analyser analyser = new(tokeniser.Tokenise());
            (IOutputSegment[] segments, Dictionary<string, string> constants, _) = analyser.Analyse();
            long romSize = 0;
            foreach (IOutputSegment segment in segments) {
                romSize = checked(romSize + segment.SizeInBytes);
                if (segment.SizeInBytes < 0 || romSize > MemoryBytes) {
                    return Serialize(new RunResult(
                        false,
                        $"ROM exceeds the playground's {MemoryBytes / 1024:N0} KiB memory limit.",
                        0
                    ));
                }
            }

            Assembler assembler = new(segments, constants);
            assembler.WriteTo(romStream);

            using StreamingTextWriter vmOutput = new(MaxOutputChars);
            Console.SetOut(vmOutput);

            CatVm vm = new(MemoryBytes, CyclesPerSecond, romStream.ToArray()) {
                Fast = true,
                EnableTestingInterrupts = true
            };

            try {
                vm.RegisterSerialDevice(0, new HardwareManager());
                bool shutdown = false;
                vm.OnShutdown += () => shutdown = true;
                long executed = 0;

                while (!shutdown && !vm.Paused) {
                    vm.ExecuteWithErrorHandling(() => vm.ExecuteInstruction(true));
                    executed++;
                }

                string? message = vm.Paused
                    ? "System halted."
                    : shutdown
                        ? "System shut down."
                        : null;

                return Serialize(new RunResult(true, message, executed));
            }
            finally {
                vm.ReleaseResources();
            }
        }
        catch (ParseException exception) {
            return Serialize(new RunResult(false, exception.Message, 0));
        }
        catch (Exception exception) {
            return Serialize(new RunResult(false, $"Runner error: {exception.Message}", 0));
        }
        finally {
            Console.SetOut(originalOutput);
            Directory.SetCurrentDirectory(originalDirectory);
            if (Directory.Exists(workspaceDirectory)) {
                Directory.Delete(workspaceDirectory, true);
            }
        }
    }

    private static Dictionary<string, string> ValidateWorkspace(Dictionary<string, string> rawFiles) {
        if (rawFiles.Count > MaxWorkspaceFiles) {
            throw new ArgumentException($"Workspace exceeds the {MaxWorkspaceFiles} file limit.");
        }

        Dictionary<string, string> files = new(StringComparer.Ordinal);
        int workspaceBytes = 0;
        foreach ((string rawPath, string contents) in rawFiles) {
            string path = NormalizeRelativePath(rawPath);
            workspaceBytes = checked(workspaceBytes + Encoding.UTF8.GetByteCount(contents));
            if (workspaceBytes > MaxWorkspaceBytes) {
                throw new ArgumentException($"Workspace exceeds the {MaxWorkspaceBytes / 1024 / 1024} MiB source limit.");
            }

            if (!files.TryAdd(path, contents)) {
                throw new ArgumentException($"Duplicate workspace path: {path}");
            }
        }

        return files;
    }

    private static string NormalizeRelativePath(string rawPath) {
        if (string.IsNullOrWhiteSpace(rawPath) || Path.IsPathRooted(rawPath)) {
            throw new ArgumentException($"Invalid workspace path: {rawPath}");
        }

        string[] parts = rawPath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part is "." or "..")) {
            throw new ArgumentException($"Invalid workspace path: {rawPath}");
        }

        return string.Join('/', parts);
    }

    private static void MaterializeWorkspace(string root, Dictionary<string, string> files) {
        Directory.CreateDirectory(root);
        foreach ((string path, string contents) in files) {
            string fullPath = Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar));
            string? directory = Path.GetDirectoryName(fullPath);
            if (directory != null) {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(fullPath, contents);
        }
    }

    private static string Serialize(RunResult result) =>
        JsonSerializer.Serialize(result, PlaygroundJsonContext.Default.RunResult);

    [JSImport("postOutput", "playground")]
    private static partial void PostOutput(string output);

    private sealed record RunResult(
        bool Success,
        string? Message,
        long Instructions
    );

    private sealed record WorkspaceFiles(Dictionary<string, string> Files);

    [JsonSerializable(typeof(RunResult))]
    [JsonSerializable(typeof(WorkspaceFiles))]
    private sealed partial class PlaygroundJsonContext : JsonSerializerContext;

    private sealed class StreamingTextWriter(int capacity) : TextWriter {
        private int _written;
        private bool _truncated;

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) {
            Write(value.ToString());
        }

        public override void Write(char[] buffer, int index, int count) {
            Write(new string(buffer, index, count));
        }

        public override void Write(string? value) {
            if (string.IsNullOrEmpty(value) || _truncated) {
                return;
            }

            int available = capacity - _written;
            if (available > 0) {
                string chunk = value.Length <= available ? value : value[..available];
                _written += chunk.Length;
                PostOutput(chunk);
            }

            if (value.Length <= available) {
                return;
            }

            _truncated = true;
            PostOutput("\n\n[Output truncated at 64 KiB]");
        }
    }
}
