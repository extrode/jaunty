using System.Collections;
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
        MemberExpression? member = ExtractMemberExpression(columnSelector.Body);
        if (member?.Member is not PropertyInfo prop)
            throw new ArgumentException("Column selector must be a property access expression.", nameof(columnSelector));

        return GetColumnName(prop);
    }

    /// <summary>
    /// AUD-R26-067: the fourth copy of the "[Column] name or property name" rule, now shared with
    /// <see cref="ColumnMappingCache"/> and <see cref="Import.TargetDdlGenerator"/> via
    /// <see cref="MappedPropertyFilter"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is deliberately <em>not</em> guarded by <see cref="MappedPropertyFilter.IsMapped"/>, even
    /// though the finding asked for that and a first attempt added it. Measured: the guard is a
    /// behavioural regression. The registered view exposes every column in the <em>file</em> -
    /// <c>DuckDb.GenerateViewSqlWithDateTimeCasts</c> builds its select list from the reader's own
    /// field names, not from the entity's mapping - so a property marked <c>[Ignore]</c> ("do not
    /// materialise this") whose column is nevertheless present in the CSV is legitimately usable in
    /// an <c>Update</c>/<c>Delete</c> predicate today. Measured before and after: an
    /// <c>[Ignore]</c>d <c>Audited</c> property over a CSV with an <c>Audited</c> column gives
    /// <c>Delete&lt;Row&gt;(r =&gt; r.Audited == "yes")</c> → 1 row deleted without the guard, and
    /// <c>InvalidOperationException</c> with it.
    /// </para>
    /// <para>
    /// The finding's own measurement used a property with no corresponding file column, where the
    /// provider does reject the SQL - so its complaint (an opaque
    /// <c>Binder Error: Referenced column "Secret" not found in FROM clause!</c> naming neither the
    /// entity nor the reason) is real but narrower than the guard. Closing it properly means
    /// deciding whether the entity's mapping or the file's columns define the queryable surface
    /// here, which is a design decision rather than a fix; carried to round 27.
    /// </para>
    /// </remarks>
    private static string GetColumnName(PropertyInfo prop) => MappedPropertyFilter.GetColumnName(prop);

    // AUD-R12-127: mirrors DuckDbDialect's private QuoteIdentifier escaping. Callers of
    // ResolveColumnName/ResolveColumnFromMember outside this file (e.g. DuckDbUpdate.cs) escape
    // the raw name themselves via _dialect.EscapeColumnName, so this helper is applied only at
    // this file's own SQL-interpolation sites rather than folded into GetColumnName - doing the
    // latter would double-escape those external callers.
    private static string EscapeColumnName(string columnName) => columnName.Replace("\"", "\"\"");

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
        return $"\"{EscapeColumnName(columnName)}\" = true";
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

        (string? columnName, object? value, bool swapped) = ExtractColumnAndValue(binary);
        ExpressionType nodeType = swapped ? Mirror(binary.NodeType) : binary.NodeType;

        if (value is null)
        {
            // A relational comparison (<, <=, >, >=) against NULL is UNKNOWN in SQL and false
            // for C#'s lifted operators - never true either way - so it becomes a match-nothing
            // predicate; only ==/!= translate to the IS NULL forms.
            if (nodeType is not (ExpressionType.Equal or ExpressionType.NotEqual))
                return "1 = 0";

            var nullOp = nodeType == ExpressionType.Equal ? "IS NULL" : "IS NOT NULL";
            return $"\"{EscapeColumnName(columnName!)}\" {nullOp}";
        }

        var sqlOp = nodeType switch
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
        return $"\"{EscapeColumnName(columnName!)}\" {sqlOp} ${paramIndex}";
    }

    private static string VisitMethodCall(MethodCallExpression method, List<DuckDBParameter> parameters, int paramOffset)
    {
        if (method.Object is MemberExpression member && method.Method.DeclaringType == typeof(string))
        {
            var columnName = ResolveColumnFromMember(member);
            var value = EvaluateExpression(method.Arguments[0]);
            var caseInsensitive = IsCaseInsensitiveComparison(method);

            return method.Method.Name switch
            {
                "Contains" => HandleStringContains(columnName, value, parameters, paramOffset, caseInsensitive),
                "StartsWith" => HandleStringStartsWith(columnName, value, parameters, paramOffset, caseInsensitive),
                "EndsWith" => HandleStringEndsWith(columnName, value, parameters, paramOffset, caseInsensitive),
                _ => throw new NotSupportedException($"String method '{method.Method.Name}' is not supported.")
            };
        }

        if (method.Method.Name == "Contains" && method.Method.DeclaringType != null &&
            (method.Method.DeclaringType == typeof(Enumerable) ||
             method.Method.DeclaringType == typeof(MemoryExtensions) ||
             method.Method.DeclaringType.IsGenericType && method.Method.DeclaringType.GetGenericTypeDefinition() == typeof(List<>)))
        {
            return HandleInClause(method, parameters, paramOffset);
        }

        throw new NotSupportedException($"Method '{method.Method.Name}' is not supported in flat file predicates.");
    }

    private static string HandleStringContains(string columnName, object? value, List<DuckDBParameter> parameters, int paramOffset, bool caseInsensitive)
    {
        var paramIndex = paramOffset + parameters.Count + 1;
        parameters.Add(new DuckDBParameter { Value = $"%{EscapeLikeValue(value)}%" });
        return $"\"{EscapeColumnName(columnName)}\" {LikeOperator(caseInsensitive)} ${paramIndex} ESCAPE '\\'";
    }

    private static string HandleStringStartsWith(string columnName, object? value, List<DuckDBParameter> parameters, int paramOffset, bool caseInsensitive)
    {
        var paramIndex = paramOffset + parameters.Count + 1;
        parameters.Add(new DuckDBParameter { Value = $"{EscapeLikeValue(value)}%" });
        return $"\"{EscapeColumnName(columnName)}\" {LikeOperator(caseInsensitive)} ${paramIndex} ESCAPE '\\'";
    }

    private static string HandleStringEndsWith(string columnName, object? value, List<DuckDBParameter> parameters, int paramOffset, bool caseInsensitive)
    {
        var paramIndex = paramOffset + parameters.Count + 1;
        parameters.Add(new DuckDBParameter { Value = $"%{EscapeLikeValue(value)}" });
        return $"\"{EscapeColumnName(columnName)}\" {LikeOperator(caseInsensitive)} ${paramIndex} ESCAPE '\\'";
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="method"/> is one of the
    /// <c>(string, StringComparison)</c> overloads and the comparison requested is case-insensitive.
    /// </summary>
    /// <remarks>
    /// AUD-R25: the three string handlers read <c>Arguments[0]</c> and emitted a bare LIKE, silently
    /// discarding the comparison argument - so
    /// <c>Where(p =&gt; p.Name.Contains("abc", StringComparison.OrdinalIgnoreCase))</c> compiled, ran,
    /// and filtered case-sensitively, because DuckDB's LIKE is case-sensitive like PostgreSQL's.
    /// Jaunty.Fluent's WhereExpressionVisitor had the identical omission and is fixed alongside this.
    ///
    /// <para>
    /// The culture distinction between Ordinal, CurrentCulture and InvariantCulture is not
    /// expressible here and is deliberately not attempted; case sensitivity is the part that changes
    /// which rows come back.
    /// </para>
    /// </remarks>
    private static bool IsCaseInsensitiveComparison(MethodCallExpression method)
    {
        if (method.Arguments.Count < 2) return false;

        return EvaluateExpression(method.Arguments[1]) is StringComparison comparison
            && comparison is StringComparison.OrdinalIgnoreCase
                or StringComparison.CurrentCultureIgnoreCase
                or StringComparison.InvariantCultureIgnoreCase;
    }

    // DuckDB follows PostgreSQL: LIKE is case-sensitive, ILIKE is not. Same pairing DuckDbDialect
    // exposes as GenerateCaseSensitiveLike/GenerateCaseInsensitiveLike.
    private static string LikeOperator(bool caseInsensitive) => caseInsensitive ? "ILIKE" : "LIKE";

    /// <summary>
    /// Escapes LIKE wildcard characters (<c>%</c>, <c>_</c>) and the escape character itself
    /// (<c>\</c>) in a value so it matches literally rather than as a wildcard pattern, when
    /// combined with an <c>ESCAPE '\'</c> clause.
    /// </summary>
    private static string EscapeLikeValue(object? value)
    {
        var text = value?.ToString() ?? string.Empty;
        return text
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }

    private static string HandleInClause(MethodCallExpression method, List<DuckDBParameter> parameters, int paramOffset)
    {
        Expression collectionExpr;
        Expression itemExpr;

        if (method.Method.DeclaringType == typeof(Enumerable) || method.Method.DeclaringType == typeof(MemoryExtensions))
        {
            // For an array, `array.Contains(x)` resolves to the span-based
            // MemoryExtensions.Contains(ReadOnlySpan<T>, T) overload (preferred over
            // Enumerable.Contains since C# started favoring first-class Span conversions), and
            // the compiler wraps the array argument in an implicit `T[] -> ReadOnlySpan<T>`
            // conversion call. ReadOnlySpan<T> is a ref struct and can't be evaluated/boxed by
            // EvaluateExpression, so unwrap back to the original array expression first.
            collectionExpr = UnwrapSpanConversion(method.Arguments[0]);
            itemExpr = method.Arguments[1];
        }
        else
        {
            collectionExpr = method.Object!;
            itemExpr = method.Arguments[0];
        }

        MemberExpression? memberExpr = ExtractMemberExpression(itemExpr);
        if (memberExpr is null)
            throw new NotSupportedException("IN clause requires a property access on the entity.");

        var columnName = ResolveColumnFromMember(memberExpr);
        IEnumerable collection = EvaluateExpression(collectionExpr) as System.Collections.IEnumerable
            ?? throw new NotSupportedException("IN clause requires an enumerable collection.");

        var sb = new StringBuilder();
        var first = true;
        foreach (var item in collection)
        {
            if (first)
            {
                sb.Append($"\"{EscapeColumnName(columnName)}\" IN (");
                first = false;
            }
            else
            {
                sb.Append(", ");
            }

            var paramIndex = paramOffset + parameters.Count + 1;
            parameters.Add(new DuckDBParameter { Value = item });
            sb.Append($"${paramIndex}");
        }

        if (first)
        {
            // Empty collection: no value can ever match, so the clause must always be false
            // rather than emitting the invalid SQL `"col" IN ()`.
            return "1 = 0";
        }

        sb.Append(')');
        return sb.ToString();
    }

    private static Expression UnwrapSpanConversion(Expression expr)
    {
        if (expr is MethodCallExpression { Method.Name: "op_Implicit" } call &&
            call.Method.DeclaringType is { IsGenericType: true } declaringType &&
            (declaringType.GetGenericTypeDefinition() == typeof(ReadOnlySpan<>) ||
             declaringType.GetGenericTypeDefinition() == typeof(Span<>)))
        {
            return call.Arguments[0];
        }

        return expr;
    }

    /// <summary>
    /// AUD-R33-001: also reports whether the operands were swapped. The caller emits
    /// <c>"column" op $n</c>, so when the entity member is on the right - <c>1000 &lt; x.Revenue</c> -
    /// the column and the value change places and the operator has to be mirrored with them.
    /// <c>=</c> and <c>!=</c> are symmetric and unaffected; <c>&lt;</c> <c>&lt;=</c> <c>&gt;</c>
    /// <c>&gt;=</c> all produced the exact inverse of the predicate before this.
    /// </summary>
    private static (string ColumnName, object? Value, bool Swapped) ExtractColumnAndValue(BinaryExpression binary)
    {
        MemberExpression? leftMember = ExtractMemberExpression(binary.Left);
        MemberExpression? rightMember = ExtractMemberExpression(binary.Right);

        if (leftMember != null && IsEntityMember(leftMember))
        {
            var columnName = ResolveColumnFromMember(leftMember);
            var value = EvaluateExpression(binary.Right);
            return (columnName, value, false);
        }

        if (rightMember != null && IsEntityMember(rightMember))
        {
            var columnName = ResolveColumnFromMember(rightMember);
            var value = EvaluateExpression(binary.Left);
            return (columnName, value, true);
        }

        throw new NotSupportedException("Binary comparison must have at least one property access on the entity.");
    }

    /// <summary>Mirrors a relational comparison so it reads correctly with the operands reversed.</summary>
    private static ExpressionType Mirror(ExpressionType nodeType) => nodeType switch
    {
        ExpressionType.LessThan => ExpressionType.GreaterThan,
        ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
        ExpressionType.GreaterThan => ExpressionType.LessThan,
        ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
        _ => nodeType
    };

    private static bool IsEntityMember(MemberExpression member)
    {
        Expression? current = member.Expression;
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
        // Unwrapped before the fast path, exactly as before: the compile fallback below would
        // otherwise apply the conversion that this deliberately discards.
        if (expression is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            return EvaluateExpression(unary.Operand);

        if (TryEvaluateWithoutCompiling(expression, out object? value))
            return value;

        // Not cached by expression.ToString(): closure-captured variables (e.g. `x => x.Age > someLocalVar`)
        // produce a new Expression instance per call but stringify identically across calls, so a
        // string-keyed cache would return a stale compiled delegate bound to an earlier call's captured value.
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<object?>>(System.Linq.Expressions.Expression.Convert(expression, typeof(object)));
        return lambda.Compile()();
    }

    /// <summary>
    /// Reads the operand's value directly where that is possible, so the compile fallback above
    /// only fires for genuinely computed operands - AUD-R25.
    /// </summary>
    /// <remarks>
    /// A closure-captured local (<c>x =&gt; x.Age &gt; minAge</c>) is not a
    /// <see cref="ConstantExpression"/>: the compiler lifts it onto a generated closure class, so
    /// it arrives as a <see cref="MemberExpression"/> - a field read - over a constant holding the
    /// closure instance, and used to miss the fast path entirely. Each such value then cost a full
    /// expression compile: a <c>DynamicMethod</c> emit of tens to hundreds of microseconds plus
    /// code heap that is never reclaimed, where reading the field is a handful of nanoseconds. A
    /// predicate capturing five values paid it five times, per Delete/Update call.
    ///
    /// <para>
    /// Jaunty.Fluent's <c>ExpressionEvaluator</c> carries the same logic for the seven copies that
    /// lived in that assembly. This one is duplicated rather than shared because the two assemblies
    /// are independent - Jaunty.FlatFiles.DuckDB does not reference Jaunty.Fluent.
    /// </para>
    /// </remarks>
    private static bool TryEvaluateWithoutCompiling(Expression expression, out object? value)
    {
        value = null;

        switch (expression)
        {
            case ConstantExpression constant:
                value = constant.Value;
                return true;

            // Only reachable in a nested position (the declaring object of a member read); the
            // top-level case is unwrapped by EvaluateExpression before this is called.
            case UnaryExpression { NodeType: ExpressionType.Convert } unary:
                return TryEvaluateWithoutCompiling(unary.Operand, out value);

            case MemberExpression member:
                object? instance = null;

                // A null Expression means a static member. Otherwise the declaring object must
                // itself be readable without compiling, or there is nothing to be gained.
                if (member.Expression is not null && !TryEvaluateWithoutCompiling(member.Expression, out instance))
                    return false;

                switch (member.Member)
                {
                    case FieldInfo field:
                        // A null instance on an instance member would throw TargetException here
                        // but NullReferenceException from compiled code; leave those to the
                        // compiled path so the failure does not depend on the route taken.
                        if (instance is null && !field.IsStatic)
                            return false;

                        value = field.GetValue(instance);
                        return true;

                    case PropertyInfo property:
                        MethodInfo? getter = property.GetGetMethod(nonPublic: true);

                        if (getter is null || property.GetIndexParameters().Length != 0)
                            return false;

                        if (instance is null && !getter.IsStatic)
                            return false;

                        value = property.GetValue(instance);
                        return true;

                    default:
                        return false;
                }

            default:
                return false;
        }
    }
}