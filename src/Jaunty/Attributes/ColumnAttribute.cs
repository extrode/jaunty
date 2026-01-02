namespace Jaunty.Attributes;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ColumnAttribute : Attribute
{
    public string Name { get; }

    public ColumnAttribute(string name) => Name = name ?? throw new ArgumentNullException(nameof(name));
}
