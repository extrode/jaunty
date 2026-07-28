using System.Globalization;
using System.Linq.Expressions;

using Jaunty.Dialects;

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
    /// constants). Shared by <c>GroupByExpressionVisitor.FormatConstant</c> and
    /// <c>SelectExpressionVisitor.FormatConstant</c>, which both delegate here instead of
    /// duplicating this logic. HAVING comparison operands are bound as query parameters instead -
    /// see <c>GroupedQueryBuilder.AddHavingParameter</c> and
    /// <c>JoinedGroupByExpressionVisitor.AddHavingParameter</c>.
    /// </summary>
    public static string FormatLiteral(object? value, ISqlDialect dialect)
    {
        return value switch
        {
            null => "NULL",
            string s => $"'{dialect.EscapeStringLiteral(s)}'",
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt:yyyy-MM-dd HH:mm:ss}'",
            // Numeric types (decimal/double/float/int/...) implement IFormattable - format with
            // the invariant culture so a comma-decimal culture (e.g. de-DE) doesn't corrupt the
            // generated SQL by rendering "1,5" instead of "1.5".
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "NULL"
        };
    }

    // AUD-R25: the implementation moved to ExpressionEvaluator, which the six expression visitors
    // now share too - this was one of eight byte-identical copies. Kept here as a forwarder for the
    // HAVING call sites that already name it.
    public static object? EvaluateExpression(Expression expression) => ExpressionEvaluator.Evaluate(expression);
}
