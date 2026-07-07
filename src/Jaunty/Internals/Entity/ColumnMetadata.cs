using System;
using System.Reflection;

using Jaunty.Attributes;

namespace Jaunty.Internals.Entity;

/// <summary>
/// Metadata for a property-to-column mapping.
/// </summary>
internal sealed class ColumnMetadata
{
    /// <summary>
    /// Gets the property info for the column, when resolved via runtime reflection
    /// (<c>Jaunty.Extensions.Reflection</c>). <see langword="null"/> when resolved from
    /// source-generated metadata instead — use <see cref="PropertyName"/>/<see cref="PropertyType"/>
    /// for name/type, and <see cref="Getter"/>/<see cref="Setter"/> for value access, which are
    /// populated regardless of which path resolved this column.
    /// </summary>
    public PropertyInfo? Property { get; }

    /// <summary>
    /// Gets the CLR property name for this column. Always populated, regardless of whether
    /// metadata was resolved via reflection or source generation.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the CLR property type for this column. Always populated, regardless of whether
    /// metadata was resolved via reflection or source generation.
    /// </summary>
    public Type PropertyType { get; }

    /// <summary>
    /// Gets a compiled, reflection-free getter for this column's value, when resolved from
    /// source-generated metadata. <see langword="null"/> when resolved via runtime reflection —
    /// use <see cref="Property"/>'s <c>GetValue</c> instead in that case.
    /// </summary>
    public Func<object, object?>? Getter { get; }

    /// <summary>
    /// Gets a compiled, reflection-free setter for this column's value, when resolved from
    /// source-generated metadata. <see langword="null"/> when resolved via runtime reflection —
    /// use <see cref="Property"/>'s <c>SetValue</c> instead in that case.
    /// </summary>
    public Action<object, object?>? Setter { get; }

    /// <summary>
    /// Gets the database column name.
    /// </summary>
    public string ColumnName { get; }

    /// <summary>
    /// Gets a value indicating whether this column is part of the primary key.
    /// </summary>
    public bool IsPrimaryKey { get; }

    /// <summary>
    /// Gets a value indicating whether this column is an identity column.
    /// </summary>
    public bool IsIdentity { get; }

    /// <summary>
    /// Gets a value indicating whether this column is computed.
    /// </summary>
    public bool IsComputed { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ColumnMetadata"/> class from a
    /// reflection-resolved <see cref="PropertyInfo"/>.
    /// </summary>
    /// <param name="property">The property info.</param>
    /// <param name="columnName">The database column name.</param>
    /// <param name="isPrimaryKey">Whether the column is part of the primary key.</param>
    /// <param name="databaseGeneratedOption">The database generated option.</param>
    public ColumnMetadata(PropertyInfo property, string columnName, bool isPrimaryKey, DatabaseGeneratedOption? databaseGeneratedOption)
    {
        Property = property;
        PropertyName = property.Name;
        PropertyType = property.PropertyType;
        ColumnName = columnName;
        IsPrimaryKey = isPrimaryKey;
        IsIdentity = databaseGeneratedOption == DatabaseGeneratedOption.Identity;
        IsComputed = databaseGeneratedOption == DatabaseGeneratedOption.Computed;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ColumnMetadata"/> class from
    /// source-generated metadata, with no <see cref="PropertyInfo"/> involved.
    /// </summary>
    /// <param name="propertyName">The CLR property name.</param>
    /// <param name="propertyType">The CLR property type.</param>
    /// <param name="columnName">The database column name.</param>
    /// <param name="isPrimaryKey">Whether the column is part of the primary key.</param>
    /// <param name="isIdentity">Whether the column is an identity column.</param>
    /// <param name="getter">A compiled, reflection-free getter for this column's value.</param>
    /// <param name="setter">A compiled, reflection-free setter for this column's value.</param>
    public ColumnMetadata(string propertyName, Type propertyType, string columnName, bool isPrimaryKey, bool isIdentity, Func<object, object?> getter, Action<object, object?> setter)
    {
        PropertyName = propertyName;
        PropertyType = propertyType;
        ColumnName = columnName;
        IsPrimaryKey = isPrimaryKey;
        IsIdentity = isIdentity;
        IsComputed = false;
        Getter = getter;
        Setter = setter;
    }
}