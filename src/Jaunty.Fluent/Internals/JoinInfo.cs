namespace Jaunty.Fluent.Internals;

/// <summary>
/// Information about a single JOIN clause.
/// </summary>
internal readonly struct JoinInfo
{
    public readonly JoinType JoinType;
    public readonly string TableName;
    public readonly string? SchemaName;
    public readonly string? Alias;
    public readonly string OnCondition;

    public JoinInfo(JoinType joinType, string tableName, string? schemaName, string? alias, string onCondition)
    {
        JoinType = joinType;
        TableName = tableName;
        SchemaName = schemaName;
        Alias = alias;
        OnCondition = onCondition;
    }

    public string JoinKeyword => JoinType switch
    {
        JoinType.Inner => "INNER JOIN",
        JoinType.Left => "LEFT JOIN",
        JoinType.Right => "RIGHT JOIN",
        _ => "JOIN"
    };
}
