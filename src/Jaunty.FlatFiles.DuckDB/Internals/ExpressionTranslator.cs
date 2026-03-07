using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using DuckDB.NET.Data;

using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB.Internals;

/// <summary>
/// Translates C# lambda expressions to DuckDB SQL WHERE clauses.
/// Uses positional parameters ($1, $2, ...) which are 1-based.
/// </summary>
/// <remarks>
/// <para><b>Supported predicate patterns:</b></para>
/// <list type="bullet">
///   <item>Comparison operators: <c>==</c>, <c>!=</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c></item>
///   <item>Logical operators: <c>&amp;&amp;</c> (AND), <c>||</c> (OR), <c>!</c> (NOT)</item>
///   <item>Null checks: <c>x.Prop == null</c> → <c>IS NULL</c>, <c>x.Prop != null</c> → <c>IS NOT NULL</c></item>
///   <item>Boolean properties: <c>x.IsActive</c> → <c>"IsActive" = true</c></item>
///   <item>String methods: <c>x.Name.Contains("foo")</c>, <c>StartsWith</c>, <c>EndsWith</c> → <c>LIKE</c></item>
///   <item>IN clauses: <c>list.Contains(x.Id)</c> or <c>Enumerable.Contains(list, x.Id)</c></item>
///   <item>Closure/captured variables: evaluated via compiled expression cache</item>
/// </list>
/// <para><b>Not supported:</b> nested method calls, arithmetic expressions, property-to-property comparisons,
/// custom method translations. Unsupported patterns throw <see cref="NotSupportedException"/>.</para>
/// </remarks>
internal static class ExpressionTranslator
{
    /// <summary>
    /// Cache for compiled expression delegates to avoid repeated compilation.
    /// Uses Expression string representation as key since Expression doesn't override GetHashCode.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Func<object?>> _expressionCache = new();

    /// <summary>
    /// Translates a predicate expression into a DuckDB WHERE clause with positional parameters.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="predicate">The predicate expression to translate.</param>
    /// <param name="paramOffset">Starting offset for parameter numbering (default: 0).</param>
    /// <returns>The SQL WHERE clause and list of parameters.</returns>
    /// <exception cref="NotSupportedException">Thrown when the expression contains unsupported patterns.</exception>
    public static (string Sql, List<DuckDBParameter> Parameters) Translate<T>(
        Expression<Func<T, bool>> predicate,
        int paramOffset = 0)
    {
        var parameters = new List<DuckDBParameter>();
        var sql = VisitExpression(predicate.Body, parameters, paramOffset);
        return (sql, parameters);
    }

    /// <summary>
    /// Resolves the column name from a property selector expression, respecting [Column] attributes.
    /// </summary>
    public static string ResolveColumnName<T>(Expression<Func<T, object>> columnSelector)
    {
        var member = ExtractMemberExpression(columnSelector.Body);
        if (member?.Member is not PropertyInfo prop)
            throw new ArgumentException("Column selector must be a property access expression.", nameof(columnSelector));

        return GetColumnName(prop);
    }

    private static string GetColumnName(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<ColumnAttribute>();
        return attr?.Name ?? prop.Name;
    }

    private static MemberExpression? ExtractMemberExpression(Expression expression)
    {
        return expression switch
        {
            MemberExpression member => member,
            UnaryExpression { NodeType: ExpressionType.Convert } unary => ExtractMemberExpression(unary.Operand),
            _ => null
        };
    }

    private static string VisitExpression(Expression expression, List<DuckDBParameter> parameters, int paramOffset)
    {
        return expression switch
        {
            BinaryExpression binary => VisitBinary(binary, parameters, paramOffset),
            MethodCallExpression method => VisitMethodCall(method, parameters, paramOffset),
            UnaryExpression { NodeType: ExpressionType.Not } unary => $"NOT ({VisitExpression(unary.Operand, parameters, paramOffset)})",
            MemberExpression member when member.Type == typeof(bool) => VisitBoolMember(member),
            _ => throw new NotSupportedException($"Expression type '{expression.NodeType}' is not supported in flat file predicates.")
        };
    }

    private static string VisitBoolMember(MemberExpression member)
    {
        var columnName = ResolveColumnFromMember(member);
        return $"\"{columnName}\" = true";
    }

    private static string VisitBinary(BinaryExpression binary, List<DuckDBParameter> parameters, int paramOffset)
    {
        if (binary.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            var left = VisitExpression(binary.Left, parameters, paramOffset);
            var right = VisitExpression(binary.Right, parameters, paramOffset);
            var op = binary.NodeType == ExpressionType.AndAlso ? "AND" : "OR";
            return $"({left} {op} {right})";
        }

        var (columnName, value) = ExtractColumnAndValue(binary);

        if (value is null)
        {
            var nullOp = binary.NodeType == ExpressionType.Equal ? "IS NULL" : "IS NOT NULL";
            return $"\"{columnName}\" {nullOp}";
        }

        var sqlOp = binary.NodeType switch
        {
            ExpressionType.Equal => "=",
            ExpressionType.NotEqual => "!=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            _ => throw new NotSupportedException($"Binary operator '{binary.NodeType}' is not supported.")
        };

        var paramIndex = paramOffset + parameters.Count + 1;
        parameters.Add(new DuckDBParameter { Value = value });
        return $"\"{columnName}\" {sqlOp} ${paramIndex}";
    }

    private static string VisitMethodCall(MethodCallExpression method, List<DuckDBParameter> parameters, int paramOffset)
    {
        if (method.Object is MemberExpression member && method.Method.DeclaringType == typeof(string))
        {
            var columnName = ResolveColumnFromMember(member);
            var value = EvaluateExpression(method.Arguments[0]);

            return method.Method.Name switch
            {
                "Contains" => HandleStringContains(columnName, value, parameters, paramOffset),
                "StartsWith" => HandleStringStartsWith(columnName, value, parameters, paramOffset),
                "EndsWith" => HandleStringEndsWith(columnName, value, parameters, paramOffset),
                _ => throw new NotSupportedException($"String method '{method.Method.Name}' is not supported.")
            };
        }

        if (method.Method.Name == "Contains" && method.Method.DeclaringType != null &&
            (method.Method.DeclaringType == typeof(Enumerable) ||
             method.Method.DeclaringType.IsGenericType && method.Method.DeclaringType.GetGenericTypeDefinition() == typeof(List<>)))
        {
            return HandleInClause(method, parameters, paramOffset);
        }

        throw new NotSupportedException($"Method '{method.Method.Name}' is not supported in flat file predicates.");
    }

    private static string HandleStringContains(string columnName, object? value, List<DuckDBParameter> parameters, int paramOffset)
    {
        var paramIndex = paramOffset + parameters.Count + 1;
        parameters.Add(new DuckDBParameter { Value = $"%{value}%" });
        return $"\"{columnName}\" LIKE ${paramIndex}";
    }

    private static string HandleStringStartsWith(string columnName, object? value, List<DuckDBParameter> parameters, int paramOffset)
    {
        var paramIndex = paramOffset + parameters.Count + 1;
        parameters.Add(new DuckDBParameter { Value = $"{value}%" });
        return $"\"{columnName}\" LIKE ${paramIndex}";
    }

    private static string HandleStringEndsWith(string columnName, object? value, List<DuckDBParameter> parameters, int paramOffset)
    {
        var paramIndex = paramOffset + parameters.Count + 1;
        parameters.Add(new DuckDBParameter { Value = $"%{value}" });
        return $"\"{columnName}\" LIKE ${paramIndex}";
    }

    private static string HandleInClause(MethodCallExpression method, List<DuckDBParameter> parameters, int paramOffset)
    {
        Expression collectionExpr;
        Expression itemExpr;

        if (method.Method.DeclaringType == typeof(Enumerable))
        {
            collectionExpr = method.Arguments[0];
            itemExpr = method.Arguments[1];
        }
        else
        {
            collectionExpr = method.Object!;
            itemExpr = method.Arguments[0];
        }

        var memberExpr = ExtractMemberExpression(itemExpr);
        if (memberExpr is null)
            throw new NotSupportedException("IN clause requires a property access on the entity.");

        var columnName = ResolveColumnFromMember(memberExpr);
        var collection = EvaluateExpression(collectionExpr) as System.Collections.IEnumerable
            ?? throw new NotSupportedException("IN clause requires an enumerable collection.");

        var sb = new StringBuilder();
        sb.Append($"\"{columnName}\" IN (");
        var first = true;
        foreach (var item in collection)
        {
            if (!first) sb.Append(", ");
            var paramIndex = paramOffset + parameters.Count + 1;
            parameters.Add(new DuckDBParameter { Value = item });
            sb.Append($"${paramIndex}");
            first = false;
        }
        sb.Append(')');
        return sb.ToString();
    }

    private static (string ColumnName, object? Value) ExtractColumnAndValue(BinaryExpression binary)
    {
        var leftMember = ExtractMemberExpression(binary.Left);
        var rightMember = ExtractMemberExpression(binary.Right);

        if (leftMember != null && IsEntityMember(leftMember))
        {
            var columnName = ResolveColumnFromMember(leftMember);
            var value = EvaluateExpression(binary.Right);
            return (columnName, value);
        }

        if (rightMember != null && IsEntityMember(rightMember))
        {
            var columnName = ResolveColumnFromMember(rightMember);
            var value = EvaluateExpression(binary.Left);
            return (columnName, value);
        }

        throw new NotSupportedException("Binary comparison must have at least one property access on the entity.");
    }

    private static bool IsEntityMember(MemberExpression member)
    {
        var current = member.Expression;
        while (current is MemberExpression nested)
            current = nested.Expression;
        return current is ParameterExpression;
    }

    private static string ResolveColumnFromMember(MemberExpression member)
    {
        if (member.Member is not PropertyInfo prop)
            throw new NotSupportedException($"Member '{member.Member.Name}' is not a property.");
        return GetColumnName(prop);
    }

    private static object? EvaluateExpression(Expression expression)
    {
        if (expression is ConstantExpression constant)
            return constant.Value;

        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            return EvaluateExpression(unary.Operand);

        var cacheKey = expression.ToString() ?? throw new InvalidOperationException("Expression ToString() returned null");
        return _expressionCache.GetOrAdd(cacheKey, _ =>
        {
            var lambda = System.Linq.Expressions.Expression.Lambda<Func<object?>>(System.Linq.Expressions.Expression.Convert(expression, typeof(object)));
            return lambda.Compile();
        })();
    }
}
