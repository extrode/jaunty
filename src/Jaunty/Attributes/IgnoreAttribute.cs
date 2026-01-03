namespace Jaunty.Attributes;

/// <summary>
/// Marks a property to be ignored during mapping.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class IgnoreAttribute : Attribute
{
}
