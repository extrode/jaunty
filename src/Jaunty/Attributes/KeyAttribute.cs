namespace Jaunty.Attributes;

/// <summary>
/// Marks a property as the primary key.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class KeyAttribute : Attribute
{
}
