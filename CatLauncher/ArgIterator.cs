using System.Diagnostics.CodeAnalysis;

namespace CatLauncher;

public class ArgIterator(string[] values) {
    private int _index;

    public bool Next([NotNullWhen(true)] out string? value) {
        if (_index >= values.Length) {
            value = null;
            return false;
        }

        value = values[_index];
        _index++;
        return true;
    }

    public bool Peek([NotNullWhen(true)] out string? value) {
        if (_index >= values.Length) {
            value = null;
            return false;
        }
        
        value = values[_index];
        return true;
    }
}