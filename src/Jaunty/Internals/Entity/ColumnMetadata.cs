using System.Reflection;

using Jaunty.Attributes;

namespace Jaunty.Internals.Entity;

/// <summary>
/// Metadata for a property-to-column mapping.
/// </summary>
internal sealed class ColumnMetadata
{
    /// <summary>
    /// Gets the property info for the column.
    /// </summary>
    public PropertyInfo Property { get; }

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
    /// Initializes a new instance of the <see cref="ColumnMetadata"/> class.
    /// </summary>
    /// <param name="property">The property info.</param>
    /// <param name="columnName">The database column name.</param>
    /// <param name="isPrimaryKey">Whether the column is part of the primary key.</param>
    /// <param name="databaseGeneratedOption">The database generated option.</param>
    public ColumnMetadata(PropertyInfo property, string columnName, bool isPrimaryKey, DatabaseGeneratedOption? databaseGeneratedOption)
    {
        Property = property;
        ColumnName = columnName;
        IsPrimaryKey = isPrimaryKey;
        IsIdentity = databaseGeneratedOption == DatabaseGeneratedOption.Identity;
        IsComputed = databaseGeneratedOption == DatabaseGeneratedOption.Computed;
    }
}