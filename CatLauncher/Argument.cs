namespace CatLauncher;

public abstract class Argument(bool chainable, bool repeatable, params string[] names) {
    public readonly bool Chainable = chainable;
    public readonly bool Repeatable = repeatable;
    public readonly string[] Names = names;
    private bool _hasRepeated = false;

    public void DoParse(string name, ArgIterator args) {
        if (_hasRepeated && !Repeatable) {
            throw new ArgumentException($"Argument {Names[0]} cannot be used more than once!");
        }
        
        _hasRepeated = true;
        Parse(name, args);
    }
    
    public abstract void Parse(string name, ArgIterator args);
}
