using System.Text;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Renames every parameter in a SQL fragment/ParameterCollection with a unique prefix, so a
/// query fragment built independently (a subquery, CTE inner query, or set-operation operand)
/// can be merged into an outer query without colliding on parameter names. Shared by
/// <c>CteBuilder&lt;T&gt;</c>, <c>SetOperationBuilder&lt;T&gt;</c>, and
/// <c>QueryBuilder&lt;T&gt;</c>'s WhereInSubquery/WhereNotInSubquery, which all solve the
/// identical problem.
/// </summary>
internal static class ParameterRenamer
{
    public static (string Sql, ParameterCollection Parameters) Rename(
        string sql,
        ParameterCollection parameters,
        string prefix)
    {
        var renamedParams = new ParameterCollection();
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach ((string? name, object? value) in parameters.GetAll())
        {
            // AUD-R26-056: ':' was missing from this set and TrimStart stripped repeats.
            //
            // Without ':', an Oracle-style parameter was mis-parsed rather than renamed: paramPrefix
            // fell back to "@", baseName stayed ":p0" because TrimStart('@').TrimStart('$') strips
            // neither, the search pattern became "@:p0" and matched nothing in the SQL, and the
            // parameter was registered as "@sq0_:p0". The result is broken SQL - a placeholder the
            // outer query never binds - rather than a merge that fails loudly. Two neighbours in
            // this assembly already accept all three sigils (ParameterCollection.ToParameterObject
            // and JoinParameterName.Qualify); this was the only one that did not, and it is the one
            // that rewrites SQL text.
            //
            // TrimStart also stripped *repeated* leading sigils, so a name like "@@rowcount"
            // collapsed to base "rowcount" and produced a pattern that could not match the SQL it
            // came from. Removing exactly one sigil - as JoinParameterName.Qualify does - keeps the
            // pattern faithful to the original name.
            //
            // Not reachable through a shipped dialect today: all four return "@" from
            // ParameterPrefix. It is exactly the assumption docs/specs/008-dialect-parameter-binding
            // sets out to remove, so it is fixed with that work in view rather than found again
            // afterwards.
            bool hasSigil = name.Length > 0 && name[0] is '@' or '$' or ':';
            var paramPrefix = hasSigil ? name.Substring(0, 1) : "@";
            var baseName = hasSigil ? name.Substring(1) : name;
            var newName = $"{paramPrefix}{prefix}_{baseName}";

            replacements[paramPrefix + baseName] = newName;
            renamedParams.Add(newName, value);
        }

        return (replacements.Count == 0 ? sql : RewritePlaceholders(sql, replacements), renamedParams);
    }

    /// <summary>
    /// AUD-R35-202 and AUD-R35-203. This used to be one <c>Regex.Replace</c> per parameter over the
    /// whole fragment, each pass reading the output of the last. That was blind to context and
    /// non-atomic: a raw fragment's string literal containing a parameter's exact text -
    /// <c>WHERE Tag = '@p0'</c> - had its *value* rewritten to <c>'@sq0_p0'</c>, and a collection
    /// holding both <c>@a</c> and <c>@sq0_a</c> under prefix <c>sq0</c> renamed <c>@a</c> to
    /// <c>@sq0_a</c> and then rewrote both occurrences to <c>@sq0_sq0_a</c>, leaving the first
    /// parameter's placeholder bound to the wrong value. It was also O(N x len) with a fresh
    /// <see cref="System.Text.RegularExpressions.Regex"/> per parameter, which never hits the static
    /// pattern cache because the pattern is built from the parameter's own name.
    /// <para>
    /// One left-to-right pass instead: literals, quoted/bracketed identifiers and comments are
    /// copied verbatim, and every placeholder outside them is looked up once against the name it had
    /// on entry, so no rewrite can feed the next. The maximal run of identifier characters after the
    /// sigil is what gets looked up, which is the same boundary the old <c>(?![a-zA-Z0-9_])</c>
    /// lookahead enforced - <c>@p0x</c> is not <c>@p0</c>.
    /// </para>
    /// </summary>
    private static string RewritePlaceholders(string sql, Dictionary<string, string> replacements)
    {
        var sb = new StringBuilder(sql.Length);
        int i = 0;

        while (i < sql.Length)
        {
            char c = sql[i];

            if (c is '\'' or '"' or '`')
            {
                sb.Append(c);
                i++;

                while (i < sql.Length)
                {
                    char inner = sql[i];
                    sb.Append(inner);
                    i++;

                    if (inner != c)
                        continue;

                    // A doubled quote is an escaped quote, not the end of the literal.
                    if (i < sql.Length && sql[i] == c)
                    {
                        sb.Append(sql[i]);
                        i++;
                        continue;
                    }

                    break;
                }

                continue;
            }

            if (c == '[')
            {
                while (i < sql.Length)
                {
                    sb.Append(sql[i]);
                    i++;

                    if (sql[i - 1] == ']')
                        break;
                }

                continue;
            }

            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n')
                {
                    sb.Append(sql[i]);
                    i++;
                }

                continue;
            }

            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                sb.Append("/*");
                i += 2;

                while (i < sql.Length)
                {
                    if (sql[i] == '*' && i + 1 < sql.Length && sql[i + 1] == '/')
                    {
                        sb.Append("*/");
                        i += 2;
                        break;
                    }

                    sb.Append(sql[i]);
                    i++;
                }

                continue;
            }

            if (c is '@' or '$' or ':')
            {
                int start = i;

                while (i < sql.Length && sql[i] is '@' or '$' or ':')
                    i++;

                while (i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] == '_'))
                    i++;

                string token = sql.Substring(start, i - start);
                sb.Append(replacements.TryGetValue(token, out string? renamed) ? renamed : token);
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }
}
