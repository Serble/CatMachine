namespace CatArgs;

public abstract class Argument(bool required, bool chainable, bool repeatable, bool positional, params string[] names) {
    public readonly bool Required = required;
    public readonly bool Chainable = chainable;
    public readonly bool Repeatable = repeatable;
    public readonly bool Positional = positional;
    public readonly string[] Names = names;
    public bool HasParsed { get; private set; } = false;

    public void DoParse(string? name, ArgIterator args) {
        if (HasParsed && !Repeatable) {
            throw new ArgumentException($"Argument {Names[0]} cannot be used more than once!");
        }
        
        HasParsed = true;
        Parse(name, args);
    }
    
    /// <summary>
    /// Handler for parsing an argument
    /// Positional arguments will be called once for each time a positional argument is used
    /// </summary>
    /// <param name="name">the name used to call this, for example if the user used '--rom' the name parameter will be 'rom', if this is a positional argument name will be null</param>
    /// <param name="args">iterator over the arguments</param>
    public abstract void Parse(string? name, ArgIterator args);
}
