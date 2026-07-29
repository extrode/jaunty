using System.Linq.Expressions;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Converts Expression{Func{T1, T2, bool}} join predicates to SQL ON clauses.
/// Supports: ==, !=, &amp;&amp;, ||, and column comparisons between two tables.
/// </summary>
internal sealed class JoinExpressionVisitor<T1, T2> : ExpressionVisitor
    where T1 : new()
    where T2 : new()
{
    private readonly ISqlDialect _dialect;
    private readonly string? _alias1;
    private readonly string? _alias2;
    private readonly EntityMetadata _metadata1;
    private readonly EntityMetadata _metadata2;
    private readonly StringBuilder _sql = new();
    private readonly List<(string Name, object? Value)> _parameters = new();
    private int _parameterIndex;
    private ParameterExpression? _param1;
    private ParameterExpression? _param2;

    public JoinExpressionVisitor(ISqlDialect dialect, string? alias1, string? alias2)
    {
        _dialect = dialect;
        _alias1 = alias1;
        _alias2 = alias2;
        _metadata1 = FluentMetadataCache.GetMetadata<T1>();
        _metadata2 = FluentMetadataCache.GetMetadata<T2>();
    }

    public (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<T1, T2, bool>> predicate)
    {
        _sql.Clear();
        _parameters.Clear();
        _parameterIndex = 0;
        _param1 = predicate.Parameters[0];
        _param2 = predicate.Parameters[1];

        Visit(predicate.Body);

        return (_sql.ToString(), _parameters);
    }

    private string GetParameterName()
    {
        return $"{_dialect.ParameterPrefix}jp{_parameterIndex++}";
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _sql.Append('(');

        if (node.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            Visit(node.Left);
            _sql.Append(node.NodeType == ExpressionType.AndAlso ? " AND " : " OR ");
            Visit(node.Right);
            _sql.Append(')');
            return node;
        }

        var leftColumn = TryGetColumnExpression(node.Left);
        var rightColumn = TryGetColumnExpression(node.Right);

        // Handle null comparisons: emit IS NULL / IS NOT NULL instead of = NULL / <> NULL.
        if ((node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual))
        {
            if (leftColumn is not null && rightColumn is null && IsNullValue(node.Right))
            {
                _sql.Append(leftColumn);
                _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                _sql.Append(')');
                return node;
            }

            if (rightColumn is not null && leftColumn is null && IsNullValue(node.Left))
            {
                _sql.Append(rightColumn);
                _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
                _sql.Append(')');
                return node;
            }
        }

        if (leftColumn is not null)
        {
            _sql.Append(leftColumn);
        }
        else
        {
            Visit(node.Left);
        }

        _sql.Append(GetOperator(node.NodeType));

        if (rightColumn is not null)
        {
            _sql.Append(rightColumn);
        }
        else
        {
            Visit(node.Right);
        }

        _sql.Append(')');
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

    private static bool IsNullValue(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        if (expression is ConstantExpression constant)
            return constant.Value is null;

        // Only evaluate side-effect-free member/constant accesses to detect captured nulls.
        if (expression is MemberExpression)
            return EvaluateExpression(expression) is null;

        return false;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        var column = TryGetColumnExpression(node);
        if (column is not null)
        {
            _sql.Append(column);
            return node;
        }

        var value = EvaluateExpression(node);
        AppendValue(value);
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        AppendValue(node.Value);
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        throw new NotSupportedException($"Method '{node.Method.Name}' is not supported in JOIN expressions.");
    }

    private string? TryGetColumnExpression(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        if (expression is not MemberExpression member)
            return null;

        Expression? current = member;
        while (current is MemberExpression me)
            current = me.Expression;

        if (current is not ParameterExpression param)
            return null;

        string propertyName = member.Member.Name;
        string? alias;
        EntityMetadata metadata;
        CachedDialectMetadata cached;

        if (param == _param1)
        {
            alias = _alias1;
            metadata = _metadata1;
            cached = FluentMetadataCache.GetForDialect<T1>(_dialect);
        }
        else if (param == _param2)
        {
            alias = _alias2;
            metadata = _metadata2;
            cached = FluentMetadataCache.GetForDialect<T2>(_dialect);
        }
        else
        {
            return null;
        }

        // AUD-R26-058: this used to scan metadata.Columns linearly and then call
        // _dialect.EscapeColumnName - re-running SqlIdentifierValidator's regex match and a keyword
        // HashSet lookup - once per column reference per query build. AUD-R25 replaced exactly that
        // with the pre-escaped CachedDialectMetadata lookup and reached WhereExpressionVisitor,
        // ExistsExpressionVisitor, SelectExpressionVisitor and the arity-3 and arity-4 join
        // visitors, but not this one: the arity-2 visitor, which is the one the overwhelmingly
        // common two-table join uses. Same arity-drift pattern round 1 found on the joined
        // builders, recurring inside the fix for a different finding.
        var escaped = cached.GetColumnName(propertyName);
        var prefix = alias ?? _dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);
        return $"{prefix}.{escaped}";
    }

    private void AppendValue(object? value)
    {
        if (value is null)
        {
            _sql.Append("NULL");
            return;
        }

        var paramName = GetParameterName();
        _sql.Append(paramName);
        _parameters.Add((paramName, value));
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
        _ => throw new NotSupportedException($"Operator {nodeType} is not supported in JOIN expressions.")
    };
}