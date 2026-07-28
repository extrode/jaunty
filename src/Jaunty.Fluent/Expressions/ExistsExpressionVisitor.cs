using System.Linq.Expressions;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Fluent.Internals;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Translates a two-parameter correlation predicate (outer, subquery) => bool
/// into SQL for EXISTS subqueries.
/// </summary>
internal sealed class ExistsExpressionVisitor<TOuter, TSubquery> : ExpressionVisitor
    where TOuter : new()
    where TSubquery : new()
{
    private readonly ISqlDialect _dialect;
    private readonly EntityMetadata _outerMetadata;
    private readonly EntityMetadata _subqueryMetadata;
    private readonly string? _outerAlias;
    private readonly string _subqueryAlias;
    private readonly StringBuilder _sql = new();
    private readonly List<(string Name, object? Value)> _parameters = new();
    private readonly Dictionary<string, int> _parameterCounts;

    private ParameterExpression? _outerParam;
    private ParameterExpression? _subqueryParam;

    /// <summary>
    /// Creates the visitor. See <paramref name="outerAlias"/>/<paramref name="subqueryAlias"/>
    /// remarks below for why both are required for correct correlation.
    /// </summary>
    /// <param name="dialect">The SQL dialect used to escape identifiers.</param>
    /// <param name="outerMetadata">Entity metadata for the outer query's entity type.</param>
    /// <param name="subqueryMetadata">Entity metadata for the correlated subquery's entity type.</param>
    /// <param name="outerAlias">
    /// The alias the outer query's FROM clause was created with (e.g. via <c>db.From&lt;T&gt;("c")</c>),
    /// or <c>null</c> to fall back to the escaped table name. Outer column references must use this
    /// alias rather than the raw table name, or the generated SQL is invalid when the outer query
    /// itself uses an alias.
    /// </param>
    /// <param name="subqueryAlias">
    /// The alias assigned to the correlated subquery's FROM clause. Always required (even when
    /// <typeparamref name="TOuter"/> and <typeparamref name="TSubquery"/> differ) so a
    /// self-referencing EXISTS (TOuter == TSubquery) doesn't resolve both sides to the identical
    /// table prefix, which would make the correlation meaningless.
    /// </param>
    /// <param name="parameterCounts">
    /// Shared parameter-name counter, when supplied, so multiple correlated EXISTS clauses on
    /// the same outer query (e.g. <c>.WhereExists(...).AndExists(...)</c>) don't each generate
    /// an identically-named "@p_exists" parameter for their captured/constant operands. Omit
    /// (or pass null) for a standalone translation.
    /// </param>
    public ExistsExpressionVisitor(ISqlDialect dialect, EntityMetadata outerMetadata, EntityMetadata subqueryMetadata, string? outerAlias, string subqueryAlias, Dictionary<string, int>? parameterCounts = null)
    {
        _dialect = dialect;
        _outerMetadata = outerMetadata;
        _subqueryMetadata = subqueryMetadata;
        _outerAlias = outerAlias;
        _subqueryAlias = subqueryAlias;
        _parameterCounts = parameterCounts ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    public (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<TOuter, TSubquery, bool>> predicate)
    {
        _sql.Clear();
        _parameters.Clear();

        // Extract parameters from lambda
        _outerParam = predicate.Parameters[0];
        _subqueryParam = predicate.Parameters[1];

        Visit(predicate.Body);

        return (_sql.ToString(), _parameters);
    }

    private string GetParameterName(string baseName)
    {
        var prefix = _dialect.ParameterPrefix;
        if (_parameterCounts.TryGetValue(baseName, out int count))
        {
            _parameterCounts[baseName] = count + 1;
            return $"{prefix}{baseName}{count + 1}";
        }
        else
        {
            _parameterCounts[baseName] = 1;
            return $"{prefix}{baseName}";
        }
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _sql.Append('(');

        // Handle logical operators (&&, ||)
        if (node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            Visit(node.Left);
            _sql.Append(node.NodeType == ExpressionType.AndAlso ? " AND " : " OR ");
            Visit(node.Right);
            _sql.Append(')');
            return node;
        }

        // Handle comparison operators - need to determine which side is outer vs subquery
        (bool IsColumn, string Sql, object? Value) leftInfo = AnalyzeExpression(node.Left);
        (bool IsColumn, string Sql, object? Value) rightInfo = AnalyzeExpression(node.Right);

        // Handle null comparisons: emit IS NULL / IS NOT NULL instead of binding a NULL
        // parameter, since SQL's three-valued logic means "col = @p" with @p bound to NULL
        // never matches (same fix pattern as WhereExpressionVisitor.IsNullConstant).
        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
        {
            if (leftInfo.IsColumn && !rightInfo.IsColumn && rightInfo.Value is null)
            {
                _sql.Append(leftInfo.Sql);
                _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                _sql.Append(')');
                return node;
            }

            if (rightInfo.IsColumn && !leftInfo.IsColumn && leftInfo.Value is null)
            {
                _sql.Append(rightInfo.Sql);
                _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                _sql.Append(')');
                return node;
            }
        }

        if (leftInfo.IsColumn)
        {
            _sql.Append(leftInfo.Sql);
        }
        else
        {
            // Constant or evaluated value
            var paramName = GetParameterName("p_exists");
            _sql.Append(paramName);
            _parameters.Add((paramName, leftInfo.Value));
        }

        _sql.Append(GetOperator(node.NodeType));

        if (rightInfo.IsColumn)
        {
            _sql.Append(rightInfo.Sql);
        }
        else
        {
            // Constant or evaluated value
            var paramName = GetParameterName("p_exists");
            _sql.Append(paramName);
            _parameters.Add((paramName, rightInfo.Value));
        }

        _sql.Append(')');
        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        (bool IsColumn, string Sql, object? Value) info = AnalyzeExpression(node);
        if (info.IsColumn)
        {
            _sql.Append(info.Sql);
        }
        else
        {
            var paramName = GetParameterName("p_exists");
            _sql.Append(paramName);
            _parameters.Add((paramName, info.Value));
        }
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is null)
        {
            _sql.Append("NULL");
        }
        else
        {
            var paramName = GetParameterName("p_exists");
            _sql.Append(paramName);
            _parameters.Add((paramName, node.Value));
        }
        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            _sql.Append("NOT (");
            Visit(node.Operand);
            _sql.Append(')');
            return node;
        }

        if (node.NodeType == ExpressionType.Convert)
        {
            Visit(node.Operand);
            return node;
        }

        return base.VisitUnary(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        throw new NotSupportedException($"Method '{node.Method.Name}' is not supported in EXISTS correlation predicates.");
    }

    private (bool IsColumn, string Sql, object? Value) AnalyzeExpression(Expression expression)
    {
        // Unwrap Convert
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        if (expression is MemberExpression member)
        {
            // Check if it's a member access on the outer parameter
            ParameterExpression? root = GetRootParameter(member);
            if (root == _outerParam)
            {
                var columnName = GetColumnName(member, _outerMetadata);
                var outerPrefix = _outerAlias ?? _dialect.EscapeTableName(_outerMetadata.SchemaName, _outerMetadata.TableName);
                return (true, $"{outerPrefix}.{_dialect.EscapeColumnName(columnName)}", null);
            }

            // Check if it's a member access on the subquery parameter
            if (root == _subqueryParam)
            {
                var columnName = GetColumnName(member, _subqueryMetadata);
                return (true, $"{_subqueryAlias}.{_dialect.EscapeColumnName(columnName)}", null);
            }

            // It's a captured variable - evaluate it
            var value = EvaluateExpression(expression);
            return (false, string.Empty, value);
        }

        if (expression is ConstantExpression constant)
        {
            return (false, string.Empty, constant.Value);
        }

        // Evaluate other expressions
        var evalValue = EvaluateExpression(expression);
        return (false, string.Empty, evalValue);
    }

    private ParameterExpression? GetRootParameter(MemberExpression member)
    {
        Expression? current = member;
        while (current is MemberExpression me)
            current = me.Expression;

        return current as ParameterExpression;
    }

    private string GetColumnName(MemberExpression member, EntityMetadata metadata)
    {
        var propertyName = member.Member.Name;

        ColumnMetadata? column = metadata.Columns.FirstOrDefault(c => c.PropertyName == propertyName);
        return column?.ColumnName ?? propertyName;
    }

    // AUD-R25: this was one of eight byte-identical private copies. Kept as a one-line forwarder
    // rather than rewriting every call site, so the shared implementation - including its
    // closure-member fast path - is the only place the behaviour lives.
    private static object? EvaluateExpression(Expression expression) => ExpressionEvaluator.Evaluate(expression);

    private static string GetOperator(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.Equal => " = ",
        ExpressionType.NotEqual => " <> ",
        ExpressionType.LessThan => " < ",
        ExpressionType.LessThanOrEqual => " <= ",
        ExpressionType.GreaterThan => " > ",
        ExpressionType.GreaterThanOrEqual => " >= ",
        _ => throw new NotSupportedException($"Operator {nodeType} is not supported in EXISTS expressions.")
    };
}