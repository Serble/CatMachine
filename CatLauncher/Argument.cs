namespace CatLauncher;

public abstract class Argument(bool required, bool chainable, bool repeatable, params string[] names) {
    public readonly bool Required = required;
    public readonly bool Chainable = chainable;
    public readonly bool Repeatable = repeatable;
    public readonly string[] Names = names;
    public bool HasParsed { get; private set; } = false;

    public void DoParse(string name, ArgIterator args) {
        if (HasParsed && !Repeatable) {
            throw new ArgumentException($"Argument {Names[0]} cannot be used more than once!");
        }
        
        HasParsed = true;
        Parse(name, args);
    }
    
    public abstract void Parse(string name, ArgIterator args);
}
