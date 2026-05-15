namespace CatData;

[AttributeUsage(AttributeTargets.Constructor)]
public class CommandLineConstructableAttribute(string name, bool register = true, string[]? portValues = null)
    : Attribute {
    public string Name { get; } = name;
    public bool Register { get; } = register;
    public string[] PortValues { get; } = portValues ?? (register ? ["port"] : []);
}
