using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using DuckDB.NET.Data;

using Jaunty.Attributes;

namespace Jaunty.FlatFiles.DuckDB;

/// <summary>
/// Pre-computed column mapping with compiled getter/setter delegates.
/// Eliminates per-call reflection for property access.
/// </summary>
internal readonly struct ColumnMapping
{
    public string ColumnName { get; init; }
    public PropertyInfo Property { get; init; }
    public Func<object, object?> Getter { get; init; }
    public Action<object, object?> Setter { get; init; }
    public Type PropertyType { get; init; }
    public bool IsDateTime { get; init; }
}

/// <summary>
/// Lightweight expression-to-SQL translator for flat file CRUD operations.
/// Handles basic predicates and column selectors without depending on Jaunty.Fluent.
/// Uses DuckDB positional parameters ($1, $2, ...) which are 1-based.
/// </summary>
internal static class FlatFileExpressionHelper
{
    /// <summary>
    /// Cache for column mappings per entity type to avoid repeated reflection.
    /// </summary>
    private static readonly ConcurrentDictionary<Type, List<ColumnMapping>> _columnMappingCache = new();

    /// <summary>
    /// Cache for compiled expression delegates to avoid repeated compilation.
    /// Uses Expression string representation as key since Expression doesn't override GetHashCode.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Func<object?>> _expressionCache = new();

    /// <summary>
    /// Translates a predicate expression into a DuckDB WHERE clause with positional parameters.
    /// </summary>
    public static (string Sql, List<DuckDBParameter> Parameters) TranslatePredicate<T>(
        Expression<Func<T, bool>> predicate, int paramOffset = 0)
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

    /// <summary>
    /// Gets all column mappings for an entity type, including compiled getter/setter delegates.
    /// Uses caching to avoid repeated reflection on hot paths.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
    public static List<ColumnMapping> GetColumnMappings(Type entityType)
    {
        return _columnMappingCache.GetOrAdd(entityType, type =>
        {
            var result = new List<ColumnMapping>();
            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanRead || !prop.CanWrite) continue;

                var underlyingType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                result.Add(new ColumnMapping
                {
                    ColumnName = GetColumnName(prop),
                    Property = prop,
                    Getter = CreateGetter(prop),
                    Setter = CreateSetter(prop),
                    PropertyType = prop.PropertyType,
                    IsDateTime = underlyingType == typeof(DateTime)
                });
            }
            return result;
        });
    }

    /// <summary>
    /// Compiles a getter delegate: (object entity) => (object?)entity.Property
    /// </summary>
    private static Func<object, object?> CreateGetter(PropertyInfo prop)
    {
        var param = Expression.Parameter(typeof(object), "entity");
        var cast = Expression.Convert(param, prop.DeclaringType!);
        var access = Expression.Property(cast, prop);
        var box = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<object, object?>>(box, param).Compile();
    }

    /// <summary>
    /// Compiles a setter delegate: (object entity, object? value) => entity.Property = (PropertyType)value
    /// </summary>
    private static Action<object, object?> CreateSetter(PropertyInfo prop)
    {
        var entityParam = Expression.Parameter(typeof(object), "entity");
        var valueParam = Expression.Parameter(typeof(object), "value");
        var cast = Expression.Convert(entityParam, prop.DeclaringType!);
        var convertedValue = Expression.Convert(valueParam, prop.PropertyType);
        var assign = Expression.Assign(Expression.Property(cast, prop), convertedValue);
        return Expression.Lambda<Action<object, object?>>(assign, entityParam, valueParam).Compile();
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
        // Handle logical operators (AND, OR)
        if (binary.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            var left = VisitExpression(binary.Left, parameters, paramOffset);
            var right = VisitExpression(binary.Right, parameters, paramOffset);
            var op = binary.NodeType == ExpressionType.AndAlso ? "AND" : "OR";
            return $"({left} {op} {right})";
        }

        // Handle comparison operators
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

        // DuckDB uses 1-based positional parameters: $1, $2, ...
        var paramIndex = paramOffset + parameters.Count + 1;
        parameters.Add(new DuckDBParameter { Value = value });
        return $"\"{columnName}\" {sqlOp} ${paramIndex}";
    }

    private static string VisitMethodCall(MethodCallExpression method, List<DuckDBParameter> parameters, int paramOffset)
    {
        // Handle string.Contains, StartsWith, EndsWith
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

        // Handle Enumerable.Contains for IN clauses
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
        // Enumerable.Contains(collection, item) or collection.Contains(item)
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
        // Try left = column, right = value
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
        // Entity members have a parameter expression as root
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
        // Handle constants directly
        if (expression is ConstantExpression constant)
            return constant.Value;

        // Handle Convert expressions (e.g., boxing)
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            return EvaluateExpression(unary.Operand);

        // Compile and cache for complex expressions (closures, field access, etc.)
        // Use expression ToString() as cache key (works for simple constant/member expressions)
        var cacheKey = expression.ToString() ?? throw new InvalidOperationException("Expression ToString() returned null");
        return _expressionCache.GetOrAdd(cacheKey, _ =>
        {
            var lambda = Expression.Lambda<Func<object?>>(Expression.Convert(expression, typeof(object)));
            return lambda.Compile();
        })();
    }
}
