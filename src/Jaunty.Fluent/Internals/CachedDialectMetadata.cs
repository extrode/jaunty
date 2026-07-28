using Jaunty.Dialects;

namespace Jaunty.Fluent.Internals;

internal sealed class CachedDialectMetadata(
    string escapedTableName,
    Dictionary<string, string> escapedColumns,
    Dictionary<string, string> rawColumns,
    ISqlDialect dialect)
{
    public string EscapedTableName { get; } = escapedTableName;
    public IReadOnlyDictionary<string, string> EscapedColumns { get; } = escapedColumns;

    /// <summary>
    /// Property name to unescaped column name. The expression visitors need this alongside the
    /// escaped form because a column reference feeds two different things - the SQL text, which
    /// must be escaped, and the generated parameter name, which must not be (AUD-R25).
    /// </summary>
    public IReadOnlyDictionary<string, string> RawColumns { get; } = rawColumns;

    /// <summary>
    /// The unescaped column name for <paramref name="propertyName"/>, or the property name itself
    /// when it is not one of the entity's mapped columns - which is what the visitors' old
    /// <c>column?.ColumnName ?? propertyName</c> lookup did.
    /// </summary>
    public string GetRawColumnName(string propertyName)
    {
        return RawColumns.TryGetValue(propertyName, out var name) ? name : propertyName;
    }

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