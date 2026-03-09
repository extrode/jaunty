using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Converts Expression{Func{T1, T2, T3, T4, bool}} predicates to SQL JOIN ON or WHERE clauses.
/// </summary>
internal sealed class JoinExpressionVisitor4<T1, T2, T3, T4> : ExpressionVisitor
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    private readonly ISqlDialect _dialect;
    private readonly string? _alias1;
    private readonly string? _alias2;
    private readonly string? _alias3;
    private readonly string? _alias4;
    private readonly StringBuilder _sql = new();

    public JoinExpressionVisitor4(ISqlDialect dialect, string? alias1, string? alias2, string? alias3, string? alias4)
    {
        _dialect = dialect;
        _alias1 = alias1;
        _alias2 = alias2;
        _alias3 = alias3;
        _alias4 = alias4;
    }

    public string Translate(Expression<Func<T1, T2, T3, T4, bool>> predicate)
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
            else if (param.Type == typeof(T4))
            {
                alias = _alias4;
                metadata = FluentMetadataCache.GetMetadata<T4>();
            }

            if (metadata != null)
            {
                string propertyName = node.Member.Name;

                ColumnMetadata? column = metadata.Columns.FirstOrDefault(c => c.Property.Name == propertyName);
                string columnName = column?.ColumnName ?? propertyName;
                string escapedColumn = _dialect.EscapeColumnName(columnName);

                string prefix = alias ?? metadata.TableName;
                _sql.Append($"{prefix}.{escapedColumn}");
                return node;
            }
        }

        // Constant or other value
        object? value = EvaluateExpression(node);
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

        LambdaExpression lambda = Expression.Lambda(expression);
        Delegate compiled = lambda.Compile();
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
