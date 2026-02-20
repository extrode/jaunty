namespace Jaunty.Fluent.Internals;

internal sealed class CachedDialectMetadata(string escapedTableName, Dictionary<string, string> escapedColumns)
{
    public string EscapedTableName { get; } = escapedTableName;
    public IReadOnlyDictionary<string, string> EscapedColumns { get; } = escapedColumns;

    public string GetColumnName(string propertyName)
    {
        return EscapedColumns.TryGetValue(propertyName, out var name) ? name : propertyName;
    }

    public IEnumerable<string> ColumnNames => EscapedColumns.Values;
}
