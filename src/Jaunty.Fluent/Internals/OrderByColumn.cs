namespace Jaunty.Fluent.Internals;

/// <summary>
/// Represents an ORDER BY column specification.
/// </summary>
internal readonly struct OrderByColumn
{
    public string ColumnName { get; }
    public bool Descending { get; }

    public OrderByColumn(string columnName, bool descending)
    {
        ColumnName = columnName;
        Descending = descending;
    }
}