using System.Text;
using Catnip.Compiler.Ast;

namespace Catnip.Compiler.CodeGen;

public class AssemblyFileBuilder {
    private readonly StringBuilder _builder = new();

    /// <summary>
    /// The source location most recently emitted into this builder as a <c>#line</c> directive,
    /// used to avoid emitting a run of identical directives. Tracked per builder because the
    /// generator builds prologue, body and epilogue into separate builders and concatenates them
    /// afterwards; within a single builder the text is a linear stream, so this is always safe.
    /// </summary>
    private (string File, int Line)? _lastSourceLocation;

    public AssemblyFileBuilder Append(AssemblyFileBuilder file) {
        _builder.Append(file);
        // Adopt the appended builder's trailing location, otherwise this builder would think the
        // stream is still at its own last directive and wrongly skip the next one as a duplicate.
        if (file._lastSourceLocation != null) {
            _lastSourceLocation = file._lastSourceLocation;
        }
        return this;
    }
    
    public AssemblyFileBuilder Append(params string[] lines) {
        foreach (string line in lines) {
            _builder.AppendLine(line);
        }
        return this;
    }

    public AssemblyFileBuilder Append(bool indented = false, params string[] lines) {
        return indented ? AppendIndented(lines) : Append(lines);
    }
    
    public AssemblyFileBuilder AppendIndented(params string[] lines) {
        foreach (string line in lines) {
            _builder.AppendLine(new string(' ', 4) + line);
        }
        return this;
    }
    
    public AssemblyFileBuilder Label(string label) {
        return Append(label + ":");
    }
    
    public AssemblyFileBuilder BlankLine(int count = 1) {
        for (int i = 0; i < count; i++) {
            Append("");
        }
        return this;
    }
    
    public AssemblyFileBuilder Comment(string comment, bool indented = false) {
        return indented ? AppendIndented($"; {comment}") : Append($"; {comment}");
    }

    /// <summary>
    /// Emits a <c>#line</c> directive marking the assembly that follows as generated from the
    /// given Catnip source location, so debug symbols can map ROM addresses back to the .nip
    /// file the user actually wrote. Repeated identical locations are skipped.
    /// </summary>
    public AssemblyFileBuilder SourceLocation(FileInformation? fileInformation, bool indented = false) {
        if (fileInformation == null || fileInformation.Line < 1 || string.IsNullOrEmpty(fileInformation.File)) {
            return this;
        }

        (string File, int Line) location = (fileInformation.File, fileInformation.Line);
        if (_lastSourceLocation == location) {
            return this;
        }

        _lastSourceLocation = location;
        return Append(indented, $"#line {location.Line}, \"{EscapeAssemblyString(location.File)}\"");
    }

    /// <summary>
    /// Clears any active <c>#line</c> mapping, so generated code that has no Catnip origin (data
    /// sections, for example) is not attributed to whichever statement happened to come last.
    /// </summary>
    public AssemblyFileBuilder ClearSourceLocation() {
        if (_lastSourceLocation == null) {
            return this;
        }

        _lastSourceLocation = null;
        return Append("#line default");
    }

    private static string EscapeAssemblyString(string value) {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    public AssemblyFileBuilder Push(bool indent, string reg) {
        return Append(indent, $"push {reg}");
    }

    public AssemblyFileBuilder Push(bool indent, string reg, string comment) {
        return Append(indent, $"push {reg}  ; {comment}");
    }
    
    public AssemblyFileBuilder Pop(bool indent, string reg) {
        return Append(indent, $"pop {reg}");
    }

    public AssemblyFileBuilder Pop(bool indent, string reg, string comment) {
        return Append(indent, $"pop {reg}  ; {comment}");
    }
    
    public override string ToString() {
        return _builder.ToString();
    }
}
