using System.Globalization;
using System.Linq.Expressions;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Closure-safe HAVING expression evaluation, shared by the single-entity
/// GroupedQueryBuilder and the joined-group builders. Handles values that don't arrive as
/// a <see cref="ConstantExpression"/> - closure-captured local variables and method
/// parameters compile to a <see cref="MemberExpression"/> over a compiler-generated closure
/// class, so they must be evaluated rather than read as a literal.
/// </summary>
internal static class HavingExpressionHelpers
{
    /// <summary>
    /// Formats a literal for inline, non-parameterized use (SELECT-projection/aggregate-column
    /// constants - matches <c>GroupByExpressionVisitor.FormatConstant</c>'s single-entity
    /// behavior). HAVING comparison operands are bound as query parameters instead - see
    /// <c>GroupedQueryBuilder.AddHavingParameter</c> and
    /// <c>JoinedGroupByExpressionVisitor.AddHavingParameter</c>.
    /// </summary>
    public static string FormatLiteral(object? value)
    {
        return value switch
        {
            null => "NULL",
            string s => $"'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            // Numeric types (decimal/double/float/int/...) implement IFormattable - format with
            // the invariant culture so a comma-decimal culture (e.g. de-DE) doesn't corrupt the
            // generated SQL by rendering "1,5" instead of "1.5".
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "NULL"
        };
    }

    public static object? EvaluateExpression(Expression expression)
    {
        if (expression is ConstantExpression constant)
            return constant.Value;

        LambdaExpression lambda = Expression.Lambda(expression);
        Delegate compiled = lambda.Compile();
        return compiled.DynamicInvoke();
    }
}
