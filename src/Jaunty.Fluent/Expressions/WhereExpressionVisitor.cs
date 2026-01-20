using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Jaunty.Fluent;
using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// Converts Expression{Func{T, bool}} predicates to SQL WHERE clauses.
/// Supports: ==, !=, <, >, <=, >=, &amp;&amp;, ||, Contains, StartsWith, EndsWith, null checks
/// </summary>
internal sealed class WhereExpressionVisitor<T> : ExpressionVisitor where T : new()
{
    private readonly ISqlDialect _dialect;
    private readonly StringBuilder _sql = new();
    private readonly List<(string Name, object? Value)> _parameters = new();
    private readonly Dictionary<string, int> _parameterCounts = new(StringComparer.OrdinalIgnoreCase);

    public WhereExpressionVisitor(ISqlDialect dialect)
    {
        _dialect = dialect;
    }

    public (string Sql, List<(string Name, object? Value)> Parameters) Translate(Expression<Func<T, bool>> predicate)
    {
        _sql.Clear();
        _parameters.Clear();
        _parameterCounts.Clear();

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

        // Handle comparison operators
        var (columnName, value, isLeftColumn) = ExtractColumnAndValue(node);

        if (columnName is null)
        {
            // Both sides are values or neither is a column - fall back to evaluating
            Visit(node.Left);
            _sql.Append(GetOperator(node.NodeType));
            Visit(node.Right);
            _sql.Append(')');
            return node;
        }

        var escapedColumn = _dialect.EscapeColumnName(columnName);

        // Handle null comparisons
        if (value is null)
        {
            _sql.Append(escapedColumn);
            _sql.Append(node.NodeType == ExpressionType.Equal ? " IS NULL" : " IS NOT NULL");
            _sql.Append(')');
            return node;
        }

        _sql.Append(escapedColumn);
        _sql.Append(GetOperator(node.NodeType));
        var paramName = GetParameterName(columnName);
        _sql.Append(paramName);
        _parameters.Add((paramName, value));

        _sql.Append(')');
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Handle Sql.* functions (Coalesce, IsNull, NullIf)
        if (node.Method.DeclaringType == typeof(Sql))
        {
            return HandleSqlFunction(node);
        }

        // Handle string methods: Contains, StartsWith, EndsWith
        if (node.Object is not null && node.Object.Type == typeof(string))
        {
            var memberExpr = node.Object as MemberExpression;
            if (memberExpr is not null && IsParameterMember(memberExpr))
            {
                var columnName = GetColumnName(memberExpr);
                var escapedColumn = _dialect.EscapeColumnName(columnName);
                var value = EvaluateExpression(node.Arguments[0]);

                var paramName = GetParameterName(columnName);

                switch (node.Method.Name)
                {
                    case "Contains":
                        _sql.Append(_dialect.GenerateCaseSensitiveLike(escapedColumn, paramName, "\\"));
                        _parameters.Add((paramName, _dialect.FormatContainsPattern(value?.ToString() ?? "")));
                        return node;
                    case "StartsWith":
                        _sql.Append(_dialect.GenerateCaseSensitiveLike(escapedColumn, paramName, "\\"));
                        _parameters.Add((paramName, _dialect.FormatStartsWithPattern(value?.ToString() ?? "")));
                        return node;
                    case "EndsWith":
                        _sql.Append(_dialect.GenerateCaseSensitiveLike(escapedColumn, paramName, "\\"));
                        _parameters.Add((paramName, _dialect.FormatEndsWithPattern(value?.ToString() ?? "")));
                        return node;
                    case "Equals" when node.Arguments.Count >= 1:
                        return HandleStringEquals(node, escapedColumn, columnName, value);
                }
            }
        }

        // Handle Enumerable.Contains for IN clauses (e.g., ids.Contains(p.Id))
        if (node.Method.Name == "Contains" && node.Method.DeclaringType == typeof(Enumerable))
        {
            var collection = EvaluateExpression(node.Arguments[0]);
            var memberExpr = node.Arguments[1] as MemberExpression;

            if (memberExpr is not null && IsParameterMember(memberExpr) && collection is System.Collections.IEnumerable enumerable)
            {
                var columnName = GetColumnName(memberExpr);
                var escapedColumn = _dialect.EscapeColumnName(columnName);

                var values = enumerable.Cast<object>().ToList();
                if (values.Count == 0)
                {
                    _sql.Append("1 = 0"); // Empty collection always false
                    return node;
                }

                _sql.Append(escapedColumn);
                _sql.Append(" IN (");
                for (int i = 0; i < values.Count; i++)
                {
                    if (i > 0) _sql.Append(", ");
                    var paramName = GetParameterName($"{columnName}_{i}");
                    _sql.Append(paramName);
                    _parameters.Add((paramName, values[i]));
                }
                _sql.Append(')');
                return node;
            }
        }

        // Fallback: evaluate and use as constant
        var result = EvaluateExpression(node);
        if (result is bool boolResult)
        {
            _sql.Append(boolResult ? "1 = 1" : "1 = 0");
        }
        else
        {
            var paramName = GetParameterName("Value");
            _sql.Append(paramName);
            _parameters.Add((paramName, result));
        }
        return node;
    }

    private Expression HandleSqlFunction(MethodCallExpression node)
    {
        var methodName = node.Method.Name;

        switch (methodName)
        {
            case "Coalesce":
                return HandleCoalesce(node);
            case "IsNull":
                return HandleIsNull(node);
            case "NullIf":
                return HandleNullIf(node);
            default:
                throw new NotSupportedException($"SQL function '{methodName}' is not supported.");
        }
    }

    private Expression HandleCoalesce(MethodCallExpression node)
    {
        var arguments = new List<string>();

        foreach (var arg in node.Arguments)
        {
            arguments.Add(TranslateArgumentToSql(arg));
        }

        _sql.Append(_dialect.GenerateCoalesce(arguments.ToArray()));
        return node;
    }

    private Expression HandleIsNull(MethodCallExpression node)
    {
        var valueArg = TranslateArgumentToSql(node.Arguments[0]);
        var defaultArg = TranslateArgumentToSql(node.Arguments[1]);

        _sql.Append(_dialect.GenerateIsNull(valueArg, defaultArg));
        return node;
    }

    private Expression HandleNullIf(MethodCallExpression node)
    {
        var valueArg = TranslateArgumentToSql(node.Arguments[0]);
        var compareArg = TranslateArgumentToSql(node.Arguments[1]);

        _sql.Append(_dialect.GenerateNullIf(valueArg, compareArg));
        return node;
    }

    private string TranslateArgumentToSql(Expression arg)
    {
        // Unwrap Convert expression
        if (arg is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            arg = unary.Operand;

        // If it's a member access on the parameter, translate to column
        if (arg is MemberExpression member && IsParameterMember(member))
        {
            var columnName = GetColumnName(member);
            return _dialect.EscapeColumnName(columnName);
        }

        // Otherwise, evaluate and create a parameter
        var value = EvaluateExpression(arg);
        var paramName = GetParameterName("SqlFn");
        _parameters.Add((paramName, value));
        return paramName;
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
        // Handle boolean properties directly (e.g., p => p.IsActive)
        if (IsParameterMember(node) && node.Type == typeof(bool))
        {
            var columnName = GetColumnName(node);
            var escapedColumn = _dialect.EscapeColumnName(columnName);
            _sql.Append(escapedColumn);
            _sql.Append(" = 1");
            return node;
        }

        // If this is a member access on the parameter, it's a column reference
        if (IsParameterMember(node))
        {
            var columnName = GetColumnName(node);
            _sql.Append(_dialect.EscapeColumnName(columnName));
            return node;
        }

        // Otherwise, evaluate as a constant
        var value = EvaluateExpression(node);
        var paramName = GetParameterName("Value");
        _sql.Append(paramName);
        _parameters.Add((paramName, value));
        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        switch (node.Value)
        {
            case null:
                _sql.Append("NULL");
                break;
            case bool boolValue:
                _sql.Append(boolValue ? "1 = 1" : "1 = 0");
                break;
            default:
                {
                    var paramName = GetParameterName("Value");
                    _sql.Append(paramName);
                    _parameters.Add((paramName, node.Value));
                    break;
                }
        }
        return node;
    }

    private (string? ColumnName, object? Value, bool IsLeftColumn) ExtractColumnAndValue(BinaryExpression node)
    {
        // Try left as column
        if (TryGetColumnName(node.Left, out var leftColumn))
        {
            var rightValue = EvaluateExpression(node.Right);
            return (leftColumn, rightValue, true);
        }

        // Try right as column
        if (TryGetColumnName(node.Right, out var rightColumn))
        {
            var leftValue = EvaluateExpression(node.Left);
            return (rightColumn, leftValue, false);
        }

        return (null, null, false);
    }

    private bool TryGetColumnName(Expression expression, out string? columnName)
    {
        columnName = null;

        // Unwrap Convert
        if (expression is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
            expression = unary.Operand;

        if (expression is MemberExpression member && IsParameterMember(member))
        {
            columnName = GetColumnName(member);
            return true;
        }

        return false;
    }

    private bool IsParameterMember(MemberExpression member)
    {
        // Walk up the chain to find the parameter
        Expression? current = member;
        while (current is MemberExpression me)
            current = me.Expression;

        return current is ParameterExpression;
    }

    private string GetColumnName(MemberExpression member)
    {
        var propertyName = member.Member.Name;

        // Look up the actual column name from metadata
        var metadata = MetadataCache<T>.Metadata;
        var column = metadata.Columns.FirstOrDefault(c => c.Property.Name == propertyName);

        return column?.ColumnName ?? propertyName;
    }

    private static object? EvaluateExpression(Expression expression)
    {
        // Handle constants directly
        if (expression is ConstantExpression constant)
            return constant.Value;

        // Compile and execute
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
        _ => throw new NotSupportedException($"Operator {nodeType} is not supported in WHERE expressions.")
    };

    private Expression HandleStringEquals(MethodCallExpression node, string escapedColumn, string columnName, object? value)
    {
        var paramName = GetParameterName(columnName);
        var stringValue = value?.ToString() ?? "";

        bool isCaseInsensitive = false;
        if (node.Arguments.Count >= 2)
        {
            var comparisonArg = EvaluateExpression(node.Arguments[1]);
            if (comparisonArg is StringComparison comparison)
            {
                isCaseInsensitive = comparison is StringComparison.OrdinalIgnoreCase
                    or StringComparison.CurrentCultureIgnoreCase
                    or StringComparison.InvariantCultureIgnoreCase;
            }
        }

        if (isCaseInsensitive)
        {
            _sql.Append(_dialect.GenerateCaseInsensitiveEquals(escapedColumn, paramName));
        }
        else
        {
            _sql.Append(escapedColumn);
            _sql.Append(" = ");
            _sql.Append(paramName);
        }

        _parameters.Add((paramName, stringValue));
        return node;
    }

    private static string EscapeLikePattern(string value)
    {
        // Escape LIKE wildcards and escape character
        return value
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_")
            .Replace("[", "\\[");
    }
}
