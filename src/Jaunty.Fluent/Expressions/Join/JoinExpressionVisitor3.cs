using System.Linq.Expressions;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Converts Expression{Func{T1, T2, T3, bool}} predicates to SQL JOIN ON or WHERE clauses.
/// </summary>
internal sealed class JoinExpressionVisitor3<T1, T2, T3> : ExpressionVisitor
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    private readonly ISqlDialect _dialect;
    private readonly string? _alias1;
    private readonly string? _alias2;
    private readonly string? _alias3;
    private readonly StringBuilder _sql = new();
    private ParameterExpression? _param1;
    private ParameterExpression? _param2;
    private ParameterExpression? _param3;
    private readonly List<(string Name, object? Value)> _parameters = new();
    private int _parameterIndex;

    public JoinExpressionVisitor3(ISqlDialect dialect, string? alias1, string? alias2, string? alias3)
    {
        _dialect = dialect;
        _alias1 = alias1;
        _alias2 = alias2;
        _alias3 = alias3;
    }

    public (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<T1, T2, T3, bool>> predicate)
    {
        _sql.Clear();
        _parameters.Clear();
        _parameterIndex = 0;
        _param1 = predicate.Parameters[0];
        _param2 = predicate.Parameters[1];
        _param3 = predicate.Parameters[2];
        Visit(predicate.Body);
        return (_sql.ToString(), _parameters);
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
        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
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
            _sql.Append(leftColumn);
        else
            Visit(node.Left);

        _sql.Append(node.NodeType switch
        {
            ExpressionType.Equal => " = ",
            ExpressionType.NotEqual => " <> ",
            ExpressionType.LessThan => " < ",
            ExpressionType.LessThanOrEqual => " <= ",
            ExpressionType.GreaterThan => " > ",
            ExpressionType.GreaterThanOrEqual => " >= ",
            _ => throw new NotSupportedException($"Operator {node.NodeType} is not supported.")
        });

        if (rightColumn is not null)
            _sql.Append(rightColumn);
        else
            Visit(node.Right);

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

    protected override Expression VisitMember(MemberExpression node)
    {
        var column = TryGetColumnExpression(node);
        if (column is not null)
        {
            _sql.Append(column);
            return node;
        }

        // Constant or other value
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

        string? alias;
        EntityMetadata metadata;

        // Match by parameter reference/identity (not by Type) so self-joins resolve correctly.
        if (param == _param1)
        {
            alias = _alias1;
            metadata = FluentMetadataCache.GetMetadata<T1>();
        }
        else if (param == _param2)
        {
            alias = _alias2;
            metadata = FluentMetadataCache.GetMetadata<T2>();
        }
        else if (param == _param3)
        {
            alias = _alias3;
            metadata = FluentMetadataCache.GetMetadata<T3>();
        }
        else
        {
            return null;
        }

        var propertyName = member.Member.Name;
        ColumnMetadata? column = metadata.Columns.FirstOrDefault(c => c.PropertyName == propertyName);
        var columnName = column?.ColumnName ?? propertyName;
        var escapedColumn = _dialect.EscapeColumnName(columnName);

        var prefix = alias ?? _dialect.EscapeTableName(metadata.SchemaName, metadata.TableName);
        return $"{prefix}.{escapedColumn}";
    }

    private void AppendValue(object? value)
    {
        if (value is null)
        {
            _sql.Append("NULL");
            return;
        }

        var paramName = $"{_dialect.ParameterPrefix}jp{_parameterIndex++}";
        _sql.Append(paramName);
        _parameters.Add((paramName, value));
    }

    private bool IsNullValue(Expression expression)
    {
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        if (expression is ConstantExpression constant)
            return constant.Value is null;

        if (expression is MemberExpression && TryGetColumnExpression(expression) is null)
            return EvaluateExpression(expression) is null;

        return false;
    }

    // AUD-R25: this was one of eight byte-identical private copies. Kept as a one-line forwarder
    // rather than rewriting every call site, so the shared implementation - including its
    // closure-member fast path - is the only place the behaviour lives.
    private static object? EvaluateExpression(Expression expression) => ExpressionEvaluator.Evaluate(expression);
}