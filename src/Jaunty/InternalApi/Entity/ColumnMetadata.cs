using System.Reflection;

using Jaunty.PublicApi.Attributes;

namespace Jaunty.InternalApi.Entity;

internal sealed class ColumnMetadata(PropertyInfo property, string columnName, bool isPrimaryKey, DatabaseGeneratedOption? databaseGeneratedOption)
{
    public PropertyInfo Property { get; } = property;
    public string ColumnName { get; } = columnName;
    public bool IsPrimaryKey { get; } = isPrimaryKey;
    public bool IsIdentity { get; } = databaseGeneratedOption == DatabaseGeneratedOption.Identity;
    public bool IsComputed { get; } = databaseGeneratedOption == DatabaseGeneratedOption.Computed;
}