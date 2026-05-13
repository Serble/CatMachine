namespace CatData;

[AttributeUsage(AttributeTargets.Constructor)]
public class CommandLineConstructableAttribute(string name) : Attribute {
    public string Name { get; } = name;
}
