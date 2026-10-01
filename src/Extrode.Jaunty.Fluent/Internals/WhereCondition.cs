namespace Extrode.Jaunty.Fluent.Internals;

/// <summary>
/// Represents a single WHERE condition.
/// </summary>
/// <remarks>
/// AUD-R26-059 (batch 5, low/consistency): <see cref="Type"/> is written by all three factory
/// methods and stored on every condition in every builder. It was kept rather than deleted because
/// what it records is not redundant - see the note on <see cref="Type"/> itself - and AUD-R38-029
/// is the first reader.
/// </remarks>
internal readonly struct WhereCondition
{
    /// <summary>
    /// What produced this condition's SQL.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Read by <see cref="FoldOperand"/>. It was retained while unread because
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

    /// <summary>
    /// The SQL as an operand of a multi-condition AND/OR fold.
    /// </summary>
    /// <remarks>
    /// AUD-R38-029: the fold parenthesises each step but not each operand. Expression and column
    /// conditions are self-contained, but a raw fragment is inserted as written, so
    /// <c>Where(expr).Where("a OR b")</c> rendered <c>(expr AND a OR b)</c>, which SQL reads as
    /// <c>(expr AND a) OR b</c>.
    /// </remarks>
    public string FoldOperand => Type == WhereConditionType.Raw ? $"({Sql})" : Sql;

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