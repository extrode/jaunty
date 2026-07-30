namespace Jaunty.Fluent.Internals;

/// <summary>
/// Represents a single WHERE condition.
/// </summary>
/// <remarks>
/// AUD-R26-059 (batch 5, low/consistency): <see cref="Type"/> is written by all three factory
/// methods, stored on every condition in every builder, and read nowhere. It is kept rather than
/// deleted because what it records is not redundant - see the note on <see cref="Type"/> itself.
/// </remarks>
internal readonly struct WhereCondition
{
    /// <summary>
    /// What produced this condition's SQL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Currently unread. It is retained rather than removed because
    /// <see cref="WhereConditionType.Raw"/> records the one fact a builder would need but cannot
    /// otherwise recover: that the fragment is caller-supplied, unvalidated and unparameterized.
    /// <c>ParameterRenamer</c> rewrites raw fragments blindly today, and any future decision about
    /// whether to rename inside one, warn about one, or reject one in a context that requires
    /// generated SQL needs exactly this distinction. Deleting the field would make that information
    /// unrecoverable - the SQL string alone does not say where it came from.
    /// </para>
    /// <para>
    /// Recorded as a decision rather than left implicit: an unread field is a reasonable thing for a
    /// reader to want to delete, and this comment is the reason not to.
    /// </para>
    /// </remarks>
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