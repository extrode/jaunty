namespace Jaunty.PublicApi.Attributes;

/// <summary>
/// Specifies how the database generates values for a property.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class DatabaseGeneratedAttribute(DatabaseGeneratedOption option) : Attribute
{
    public DatabaseGeneratedOption Option { get; } = option;
}
