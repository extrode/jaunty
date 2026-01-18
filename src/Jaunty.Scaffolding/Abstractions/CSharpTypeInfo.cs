namespace Jaunty.Scaffolding.Abstractions;

/// <summary>
/// Information about a C# type mapped from a database column.
/// </summary>
public readonly struct CSharpTypeInfo
{
    /// <summary>
    /// The C# type name (e.g., "int", "string", "DateTime", "Guid").
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// Whether this is a value type (struct) as opposed to a reference type.
    /// </summary>
    public bool IsValueType { get; init; }

    /// <summary>
    /// A using directive required for this type, if any (e.g., "System" for Guid).
    /// </summary>
    public string? RequiredUsing { get; init; }
}
