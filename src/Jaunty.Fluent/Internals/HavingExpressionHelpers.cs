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
            // AUD-R32-001: was hardcoded "1"/"0". PostgreSqlDialect.FormatBooleanLiteral returns
            // TRUE/FALSE, and a bare 1/0 is not a boolean there - a projected or grouped bool
            // constant produced SQL PostgreSQL rejects. The dialect method exists for this.
            bool b => dialect.FormatBooleanLiteral(b),
            DateTime dt => $"'{dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}'",
            // AUD-R34-006: DateTime was the only quoted temporal type. Every other one implements
            // IFormattable, so it fell into the generic arm below and came out bare. DateOnly is
            // the silent case - `2026-08-02` unquoted is valid arithmetic in most dialects and
            // computes 2016 rather than failing - while TimeSpan/DateTimeOffset/Guid produce a
            // syntax error. char reached the ToString() fallback and came out as a bare identifier.
            DateTimeOffset dto => $"'{dto.ToString("yyyy-MM-dd HH:mm:sszzz", CultureInfo.InvariantCulture)}'",
            TimeSpan ts => $"'{ts.ToString("c", CultureInfo.InvariantCulture)}'",
            Guid g => $"'{g.ToString("D", CultureInfo.InvariantCulture)}'",
            char c => $"'{dialect.EscapeStringLiteral(c.ToString())}'",
#if NET8_0_OR_GREATER
            DateOnly d => $"'{d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}'",
            TimeOnly t => $"'{t.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}'",
#endif
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
