using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Names a join predicate's parameters after the column they filter, so a query carries
/// <c>@p_category_id</c> rather than the positional <c>@jp0</c>.
/// </summary>
/// <remarks>
/// <para>
/// A derived name is searchable: it appears in the SQL, in the command log and in the parameter
/// collection as the same string the caller can grep for. A counter is none of those things, and
/// two adjacent filters on unrelated columns read identically.
/// </para>
/// <para>
/// The string-based <c>Where(string column, object?)</c> overloads already derive their names, via
/// <see cref="ParameterCollection.CreateUniqueName"/>. That path keeps its unconditional
/// <c>_&lt;count&gt;</c> suffix and its one-for-one character replacement, because it has to keep
/// two distinct caller-supplied texts - <c>"p.category_id"</c> and <c>"p_category_id"</c> - from
/// collapsing onto one name (AUD-R35-014). Here the input is a column reference the builder
/// rendered itself, so there is no second text to collide with and the suffix can be spent only
/// where a query filters one column twice.
/// </para>
/// </remarks>
internal static class JoinParameterNaming
{
    /// <summary>
    /// A parameter name derived from a rendered column reference, unique within
    /// <paramref name="taken"/>.
    /// </summary>
    /// <param name="parameterPrefix">The dialect's parameter sigil, e.g. <c>"@"</c>.</param>
    /// <param name="column">The rendered column reference, e.g. <c>p.category_id</c>, or null.</param>
    /// <param name="taken">Parameters already collected for this predicate.</param>
    /// <param name="fallbackIndex">Counter backing the positional name, advanced when it is used.</param>
    internal static string Derive(
        string parameterPrefix,
        string? column,
        List<(string Name, object? Value)> taken,
        ref int fallbackIndex)
    {
        string stem = column is null ? string.Empty : Sanitize(column);

        if (stem.Length == 0)
            return parameterPrefix + "jp" + (fallbackIndex++).ToString(CultureInfo.InvariantCulture);

        string candidate = parameterPrefix + stem;
        if (!Contains(taken, candidate))
            return candidate;

        for (var suffix = 2; ; suffix++)
        {
            candidate = parameterPrefix + stem + "_" + suffix.ToString(CultureInfo.InvariantCulture);
            if (!Contains(taken, candidate))
                return candidate;
        }
    }

    /// <summary>
    /// Reduces a rendered column reference to an identifier: runs of anything that is not a letter,
    /// digit or underscore collapse to a single underscore, and leading and trailing underscores go.
    /// </summary>
    /// <remarks>
    /// Collapsing rather than replacing one-for-one is what makes the dialects agree. A column the
    /// dialect had to quote arrives as <c>[p].[order]</c> on SQL Server, <c>`p`.`order`</c> on
    /// MySQL and <c>"p"."order"</c> on PostgreSQL; all three reduce to <c>p_order</c>, so the same
    /// query names its parameters the same way whichever engine it is built for.
    /// </remarks>
    /// <param name="column">The rendered column reference.</param>
    private static string Sanitize(string column)
    {
        var sb = new StringBuilder(column.Length);
        var pendingSeparator = false;

        for (var i = 0; i < column.Length; i++)
        {
            char c = column[i];

            if (char.IsLetterOrDigit(c) || c == '_')
            {
                if (pendingSeparator && sb.Length > 0)
                    sb.Append('_');

                pendingSeparator = false;
                sb.Append(c);
            }
            else
            {
                pendingSeparator = true;
            }
        }

        return sb.ToString();
    }

    private static bool Contains(List<(string Name, object? Value)> taken, string candidate)
    {
        for (var i = 0; i < taken.Count; i++)
        {
            if (string.Equals(taken[i].Name, candidate, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
