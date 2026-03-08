using System.Linq.Expressions;
using System.Reflection;
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

    public JoinExpressionVisitor3(ISqlDialect dialect, string? alias1, string? alias2, string? alias3)
    {
        _dialect = dialect;
        _alias1 = alias1;
        _alias2 = alias2;
        _alias3 = alias3;
    }

    public string Translate(Expression<Func<T1, T2, T3, bool>> predicate)
    {
        _sql.Clear();
        Visit(predicate.Body);
        return _sql.ToString();
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _sql.Append('(');
        Visit(node.Left);

        _sql.Append(node.NodeType switch
        {
            ExpressionType.AndAlso => " AND ",
            ExpressionType.OrElse => " OR ",
            ExpressionType.Equal => " = ",
            ExpressionType.NotEqual => " <> ",
            ExpressionType.LessThan => " < ",
            ExpressionType.LessThanOrEqual => " <= ",
            ExpressionType.GreaterThan => " > ",
            ExpressionType.GreaterThanOrEqual => " >= ",
            _ => throw new NotSupportedException($"Operator {node.NodeType} is not supported.")
        });

        Visit(node.Right);
        _sql.Append(')');

        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is ParameterExpression param)
        {
            string? alias = null;
            EntityMetadata? metadata = null;

            if (param.Type == typeof(T1))
            {
                alias = _alias1;
                metadata = FluentMetadataCache.GetMetadata<T1>();
            }
            else if (param.Type == typeof(T2))
            {
                alias = _alias2;
                metadata = FluentMetadataCache.GetMetadata<T2>();
            }
            else if (param.Type == typeof(T3))
            {
                alias = _alias3;
                metadata = FluentMetadataCache.GetMetadata<T3>();
            }

            if (metadata != null)
            {
                var propertyName = node.Member.Name;
                var column = metadata.Columns.FirstOrDefault(c => c.Property.Name == propertyName);
                var columnName = column?.ColumnName ?? propertyName;
                var escapedColumn = _dialect.EscapeColumnName(columnName);

                var prefix = alias ?? metadata.TableName;
                _sql.Append($"{prefix}.{escapedColumn}");
                return node;
            }
        }

        // Constant or other value
        var value = EvaluateExpression(node);
        _sql.Append(FormatConstant(value));
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        _sql.Append(FormatConstant(node.Value));
        return node;
    }

    private static object? EvaluateExpression(Expression expression)
    {
        if (expression is ConstantExpression constant)
            return constant.Value;

        var lambda = Expression.Lambda(expression);
        var compiled = lambda.Compile();
        return compiled.DynamicInvoke();
    }

    private string FormatConstant(object? value)
    {
        return value switch
        {
            null => "NULL",
            string s => $"'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            _ => value.ToString() ?? "NULL"
        };
    }
}