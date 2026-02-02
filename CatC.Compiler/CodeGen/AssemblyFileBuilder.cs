using System.Text;

namespace CatC.Compiler.CodeGen;

public class AssemblyFileBuilder {
    private readonly StringBuilder _builder = new();

    public AssemblyFileBuilder Append(AssemblyFileBuilder file) {
        _builder.Append(file);
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

    public AssemblyFileBuilder Push(bool indent, string reg) {
        return Append(indent, $"push {reg}");
    }
    
    public AssemblyFileBuilder Pop(bool indent, string reg) {
        return Append(indent, $"pop {reg}");
    }
    
    public override string ToString() {
        return _builder.ToString();
    }
}
