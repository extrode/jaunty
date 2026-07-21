using Jaunty.Dialects;

namespace Jaunty.Fluent.Internals;

internal sealed class CachedDialectMetadata(string escapedTableName, Dictionary<string, string> escapedColumns, ISqlDialect dialect)
{
    public string EscapedTableName { get; } = escapedTableName;
    public IReadOnlyDictionary<string, string> EscapedColumns { get; } = escapedColumns;

    // propertyName is only unmapped here when it doesn't correspond to one of the entity's known
    // columns (e.g. an anonymous-object property that doesn't match T's mapped columns) - escape it
    // defensively so a raw DB-column-name collision with a reserved keyword doesn't reach the SQL
    // text unescaped. Mapped columns already come back pre-escaped from EscapedColumns.
    public string GetColumnName(string propertyName)
    {
        return EscapedColumns.TryGetValue(propertyName, out var name) ? name : dialect.EscapeColumnName(propertyName);
    }

    public IEnumerable<string> ColumnNames => EscapedColumns.Values;
}