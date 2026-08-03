using System.Linq.Expressions;

namespace Jaunty.Fluent.Expressions;

/// <summary>
/// The one place that decides whether a member expression names a column or merely a member of one.
/// </summary>
/// <remarks>
/// AUD-R35-019. AUD-R34-021 closed this hole in <c>WhereExpressionVisitor</c> alone. The check has
/// to exist wherever a member expression is turned into a column name, because the lookup it
/// guards - <c>CachedDialectMetadata.GetColumnName</c> - falls back to
/// <c>EscapeColumnName(propertyName)</c> for anything unmapped, and so quietly invents a column
/// named after the chain's leaf rather than failing.
/// <para>
/// The two sites that never got it: <c>SelectExpressionVisitor.GetEscapedColumnName</c>, so
/// <c>.Select(o =&gt; new { o.OrderDate.Year })</c> projected <c>[Year] AS Year</c> and a window
/// function's <c>PartitionBy(o =&gt; o.OrderDate.Year)</c> emitted <c>PARTITION BY [Year]</c>; and
/// <c>PropertyExtractor.GetMemberInfo</c>, which feeds join keys, ORDER BY and INSERT column lists,
/// so <c>On(p =&gt; p.OrderDate.Year, o =&gt; o.Id)</c> joined on a column called <c>Year</c>.
/// Where the entity happens to map a column named <c>Year</c>, <c>Date</c> or <c>Day</c>, the
/// wrong column was used silently rather than erroring.
/// </para>
/// <para>
/// <c>string.Length</c> and the nullable <c>.Value</c> access are the nested shapes that do have a
/// meaning; their callers unwrap to the direct inner member before arriving here, so the guard
/// never sees them.
/// </para>
/// </remarks>
internal static class ColumnReference
{
    /// <summary>
    /// Throws unless <paramref name="member"/> reads a member directly off the lambda parameter.
    /// </summary>
    /// <param name="member">The member expression about to be resolved to a column.</param>
    /// <exception cref="NotSupportedException">The receiver is another expression, so the member
    /// is a member of a column rather than a column.</exception>
    public static void RequireDirect(MemberExpression member)
    {
        if (member.Expression is null or ParameterExpression)
            return;

        string leaf = member.Member.Name;
        throw new NotSupportedException(
            $"'{member}' is a member of a column, not a column. Jaunty does not translate " +
            $"'{leaf}' into SQL - use the Sql.* helpers for the supported spellings " +
            "(Sql.Year, Sql.Month, Sql.Day, Sql.Length, ...), or compute the value in memory.");
    }

    /// <summary>
    /// Whether <paramref name="member"/> reads a member directly off the lambda parameter.
    /// </summary>
    /// <param name="member">The member expression to test.</param>
    public static bool IsDirect(MemberExpression member) =>
        member.Expression is null or ParameterExpression;
}
