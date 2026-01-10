namespace Jaunty.Attributes;

/// <summary>
/// Specifies the database table that a class is mapped to.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TableAttribute(string name, string? schema = null) : Attribute
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));

    public string? Schema { get; } = schema;
}
