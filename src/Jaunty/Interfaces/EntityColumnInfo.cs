using System;

namespace Jaunty.Interfaces;

/// <summary>
/// Describes a single property-to-column mapping for an entity, as reported by
/// <see cref="IEntityMetadataSource"/>.
/// </summary>
/// <remarks>
/// This is the public transport shape the Jaunty source generator emits directly (no reflection
/// involved on either side). Jaunty core converts instances of this struct into its internal
/// entity metadata representation.
/// </remarks>
public readonly struct EntityColumnInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EntityColumnInfo"/> struct.
    /// </summary>
    /// <param name="columnName">The database column name.</param>
    /// <param name="propertyName">The CLR property name.</param>
    /// <param name="isPrimaryKey">Whether the column is part of the primary key.</param>
    /// <param name="isIdentity">Whether the column is an identity (database-generated) column.</param>
    /// <param name="isComputed">Whether the column is database-computed and excluded from INSERT/UPDATE.</param>
    /// <param name="propertyType">The CLR property type.</param>
    /// <param name="getter">A compiled, reflection-free getter for this column's value.</param>
    /// <param name="setter">A compiled, reflection-free setter for this column's value.</param>
    public EntityColumnInfo(string columnName, string propertyName, bool isPrimaryKey, bool isIdentity, bool isComputed, Type propertyType, Func<object, object?> getter, Action<object, object?> setter)
    {
        ColumnName = columnName;
        PropertyName = propertyName;
        IsPrimaryKey = isPrimaryKey;
        IsIdentity = isIdentity;
        IsComputed = isComputed;
        PropertyType = propertyType;
        Getter = getter;
        Setter = setter;
    }

    /// <summary>
    /// Gets the database column name.
    /// </summary>
    public string ColumnName { get; }

    /// <summary>
    /// Gets the CLR property name.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets a value indicating whether the column is part of the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; }

    /// <summary>
    /// Gets a value indicating whether the column is an identity (database-generated) column.
    /// </summary>
    public bool IsIdentity { get; }

    /// <summary>
    /// Gets a value indicating whether the column is database-computed. Computed columns are
    /// excluded from generated INSERT and UPDATE statements, matching the reflection-resolved path.
    /// </summary>
    public bool IsComputed { get; }

    /// <summary>
    /// Gets the CLR property type.
    /// </summary>
    public Type PropertyType { get; }

    /// <summary>
    /// Gets a compiled, reflection-free getter for this column's value.
    /// </summary>
    public Func<object, object?> Getter { get; }

    /// <summary>
    /// Gets a compiled, reflection-free setter for this column's value.
    /// </summary>
    public Action<object, object?> Setter { get; }
}
