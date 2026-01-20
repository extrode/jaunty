using System.Linq.Expressions;
using System.Text;

using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;

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
    private readonly StringBuilder _sql = new();
    private readonly List<(string Name, object? Value)> _parameters = new();
    private readonly Dictionary<string, int> _parameterCounts = new(StringComparer.OrdinalIgnoreCase);

    private ParameterExpression? _outerParam;
    private ParameterExpression? _subqueryParam;

    public ExistsExpressionVisitor(ISqlDialect dialect, EntityMetadata outerMetadata, EntityMetadata subqueryMetadata)
    {
        _dialect = dialect;
        _outerMetadata = outerMetadata;
        _subqueryMetadata = subqueryMetadata;
    }

    public (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<TOuter, TSubquery, bool>> predicate)
    {
        _sql.Clear();
        _parameters.Clear();
        _parameterCounts.Clear();

        // Extract parameters from lambda
        _outerParam = predicate.Parameters[0];
        _subqueryParam = predicate.Parameters[1];

        Visit(predicate.Body);

        return (_sql.ToString(), _parameters);
    }

    private string GetParameterName(string baseName)
    {
        if (_parameterCounts.TryGetValue(baseName, out int count))
        {
            _parameterCounts[baseName] = count + 1;
            return $"@{baseName}{count + 1}";
        }
        else
        {
            _parameterCounts[baseName] = 1;
            return $"@{baseName}";
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
        var leftInfo = AnalyzeExpression(node.Left);
        var rightInfo = AnalyzeExpression(node.Right);

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
        var info = AnalyzeExpression(node);
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

    private (bool IsColumn, string Sql, object? Value) AnalyzeExpression(Expression expression)
    {
        // Unwrap Convert
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        if (expression is MemberExpression member)
        {
            // Check if it's a member access on the outer parameter
            var root = GetRootParameter(member);
            if (root == _outerParam)
            {
                var columnName = GetColumnName(member, _outerMetadata);
                var outerTable = _dialect.EscapeTableName(_outerMetadata.SchemaName, _outerMetadata.TableName);
                return (true, $"{outerTable}.{_dialect.EscapeColumnName(columnName)}", null);
            }

            // Check if it's a member access on the subquery parameter
            if (root == _subqueryParam)
            {
                var columnName = GetColumnName(member, _subqueryMetadata);
                var subqueryTable = _dialect.EscapeTableName(_subqueryMetadata.SchemaName, _subqueryMetadata.TableName);
                return (true, $"{subqueryTable}.{_dialect.EscapeColumnName(columnName)}", null);
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
        var column = metadata.Columns.FirstOrDefault(c => c.Property.Name == propertyName);
        return column?.ColumnName ?? propertyName;
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
        _ => throw new NotSupportedException($"Operator {nodeType} is not supported in EXISTS expressions.")
    };
}
