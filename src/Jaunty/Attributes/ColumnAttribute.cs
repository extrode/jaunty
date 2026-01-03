namespace Jaunty.Attributes;

/// <summary>
/// Specifies the database column that a property is mapped to.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ColumnAttribute(string name) : Attribute
{
    public string Name { get; } = name ?? throw new ArgumentNullException(nameof(name));
}
