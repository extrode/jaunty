using System.Reflection;

namespace Jaunty.FlatFiles.DuckDB.Internals;

/// <summary>
/// Pre-computed column mapping with compiled getter/setter delegates.
/// Eliminates per-call reflection for property access.
/// </summary>
internal readonly struct ColumnMapping
{
    /// <summary>
    /// Gets the database column name (from [Column] attribute or property name).
    /// </summary>
    public string ColumnName { get; init; }

    /// <summary>
    /// Gets the property info for reflection metadata.
    /// </summary>
    public PropertyInfo Property { get; init; }

    /// <summary>
    /// Gets the compiled getter delegate: (object entity) => (object?)entity.Property
    /// </summary>
    public Func<object, object?> Getter { get; init; }

    /// <summary>
    /// Gets the compiled setter delegate: (object entity, object? value) => entity.Property = value
    /// </summary>
    public Action<object, object?> Setter { get; init; }

    /// <summary>
    /// Gets the property type for type conversion.
    /// </summary>
    public Type PropertyType { get; init; }

    /// <summary>
    /// Gets a value indicating whether this property is a DateTime type.
    /// Used for DATE→TIMESTAMP casting during source registration.
    /// </summary>
    public bool IsDateTime { get; init; }
}
