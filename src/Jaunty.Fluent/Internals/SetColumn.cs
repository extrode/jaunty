namespace Jaunty.Fluent.Internals;

/// <summary>
/// Represents a single SET column assignment for UPDATE operations.
/// </summary>
internal readonly struct SetColumn
{
    /// <summary>
    /// The column name (already escaped).
    /// </summary>
    public string ColumnName { get; }

    /// <summary>
    /// The parameter name used in the SQL (e.g., "@p0", "@UnitPrice").
    /// </summary>
    public string ParameterName { get; }

    public SetColumn(string columnName, string parameterName)
    {
        ColumnName = columnName;
        ParameterName = parameterName;
    }
}
