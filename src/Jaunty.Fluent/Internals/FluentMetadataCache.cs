using System.Collections.Concurrent;

using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Caches dialect-specific SQL fragments for entity types.
/// Avoids repeated string escaping and column name lookups.
/// </summary>
internal static class FluentMetadataCache<T> where T : new()
{
    private static readonly ConcurrentDictionary<Type, CachedDialectMetadata> _dialectCache = new();

    /// <summary>
    /// Gets cached metadata for the specified dialect, creating it if necessary.
    /// </summary>
    public static CachedDialectMetadata GetForDialect(ISqlDialect dialect)
    {
        var dialectType = dialect.GetType();
        return _dialectCache.GetOrAdd(dialectType, _ => CreateCachedMetadata(dialect));
    }

    private static CachedDialectMetadata CreateCachedMetadata(ISqlDialect dialect)
    {
        var metadata = MetadataCache<T>.Metadata;
        var columns = metadata.Columns;

        // Cache escaped table name
        var escapedTableName = dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);

        // Cache all column names (unescaped)
        var columnNames = new string[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            columnNames[i] = columns[i].ColumnName;
        }

        // Cache escaped column names
        var escapedColumnNames = new string[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            escapedColumnNames[i] = dialect.EscapeColumnName(columns[i].ColumnName);
        }

        // Cache property name to column name mapping
        var propertyToColumn = new Dictionary<string, string>(columns.Count, StringComparer.Ordinal);
        var propertyToEscapedColumn = new Dictionary<string, string>(columns.Count, StringComparer.Ordinal);
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            propertyToColumn[col.Property.Name] = col.ColumnName;
            propertyToEscapedColumn[col.Property.Name] = escapedColumnNames[i];
        }

        return new CachedDialectMetadata(escapedTableName, columnNames, escapedColumnNames, propertyToColumn, propertyToEscapedColumn);
    }
}

/// <summary>
/// Cached SQL fragments for a specific dialect.
/// </summary>
internal sealed class CachedDialectMetadata
{
    public string EscapedTableName { get; }
    public string[] ColumnNames { get; }
    public string[] EscapedColumnNames { get; }
    private readonly Dictionary<string, string> _propertyToColumn;
    private readonly Dictionary<string, string> _propertyToEscapedColumn;

    public CachedDialectMetadata(string escapedTableName, string[] columnNames, string[] escapedColumnNames, Dictionary<string, string> propertyToColumn, Dictionary<string, string> propertyToEscapedColumn)
    {
        EscapedTableName = escapedTableName;
        ColumnNames = columnNames;
        EscapedColumnNames = escapedColumnNames;
        _propertyToColumn = propertyToColumn;
        _propertyToEscapedColumn = propertyToEscapedColumn;
    }

    /// <summary>
    /// Gets the column name for a property, or returns the property name if not found.
    /// </summary>
    public string GetColumnName(string propertyName)
    {
        return _propertyToColumn.TryGetValue(propertyName, out var columnName)
            ? columnName
            : propertyName;
    }

    /// <summary>
    /// Gets the escaped column name for a property, or escapes the property name if not found.
    /// </summary>
    public string GetEscapedColumnName(string propertyName)
    {
        return _propertyToEscapedColumn.TryGetValue(propertyName, out var escapedName)
            ? escapedName
            : propertyName;
    }
}
