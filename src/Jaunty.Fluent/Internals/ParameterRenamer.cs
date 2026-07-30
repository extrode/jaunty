using System.Text.RegularExpressions;

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
        var renamedSql = sql;

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

            // Replace in SQL - use word boundary to avoid partial matches
            var pattern = $@"{Regex.Escape(paramPrefix)}{Regex.Escape(baseName)}(?![a-zA-Z0-9_])";
            renamedSql = Regex.Replace(renamedSql, pattern, newName);

            renamedParams.Add(newName, value);
        }

        return (renamedSql, renamedParams);
    }
}
