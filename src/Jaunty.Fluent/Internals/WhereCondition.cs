namespace Jaunty.Fluent.Internals;

/// <summary>
/// Represents a single WHERE condition.
/// </summary>
internal readonly struct WhereCondition
{
    public WhereConditionType Type { get; }
    public string Sql { get; }
    public LogicalOperator Operator { get; }

    public WhereCondition(WhereConditionType type, string sql, LogicalOperator op)
    {
        Type = type;
        Sql = sql;
        Operator = op;
    }

    public static WhereCondition Column(string sql, LogicalOperator op) => new(WhereConditionType.Column, sql, op);
    public static WhereCondition Raw(string sql, LogicalOperator op) => new(WhereConditionType.Raw, sql, op);
    public static WhereCondition Expression(string sql, LogicalOperator op) => new(WhereConditionType.Expression, sql, op);
}

internal enum WhereConditionType
{
    Column,     // Simple column = value
    Raw,        // Raw SQL
    Expression  // From expression tree
}

internal enum LogicalOperator
{
    None,   // First condition (no AND/OR prefix)
    And,
    Or
}
