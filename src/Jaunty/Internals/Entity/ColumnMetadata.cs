using System.Reflection;

using Jaunty.Attributes;

namespace Jaunty.Internals.Entity;

public sealed class ColumnMetadata
{
    public PropertyInfo Property { get; }

    public string ColumnName { get; }

    public bool IsPrimaryKey { get; }

    public bool IsIdentity { get; }

    public bool IsComputed { get; }

    public ColumnMetadata(PropertyInfo property, string columnName, bool isPrimaryKey, DatabaseGeneratedOption? databaseGeneratedOption)
    {
        Property = property;
        ColumnName = columnName;
        IsPrimaryKey = isPrimaryKey;
        IsIdentity = databaseGeneratedOption == DatabaseGeneratedOption.Identity;
        IsComputed = databaseGeneratedOption == DatabaseGeneratedOption.Computed;
    }
}