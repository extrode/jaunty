using System.Linq.Expressions;
using System.Text;

using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Fluent.Internals;

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

    public string Translate(Expression<Func<T1, T2, bool>> predicate)
    {
        _sql.Clear();
        _param1 = predicate.Parameters[0];
        _param2 = predicate.Parameters[1];

        Visit(predicate.Body);

        return _sql.ToString();
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

        if (param == _param1)
        {
            alias = _alias1;
            metadata = _metadata1;
        }
        else if (param == _param2)
        {
            alias = _alias2;
            metadata = _metadata2;
        }
        else
        {
            return null;
        }

        string columnName = propertyName;
        var columns = metadata.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
            {
                columnName = columns[i].ColumnName;
                break;
            }
        }

        var escaped = _dialect.EscapeColumnName(columnName);
        var prefix = alias ?? metadata.TableName;
        return $"{prefix}.{escaped}";
    }

    private void AppendValue(object? value)
    {
        if (value is null)
        {
            _sql.Append("NULL");
        }
        else if (value is string s)
        {
            _sql.Append('\'');
            _sql.Append(s.Replace("'", "''"));
            _sql.Append('\'');
        }
        else if (value is bool b)
        {
            _sql.Append(b ? "1" : "0");
        }
        else
        {
            _sql.Append(value);
        }
    }

    private static object? EvaluateExpression(Expression expression)
    {
        if (expression is ConstantExpression constant)
            return constant.Value;

        var lambda = Expression.Lambda(expression);
        var compiled = lambda.Compile();
        return compiled.DynamicInvoke();
    }

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
