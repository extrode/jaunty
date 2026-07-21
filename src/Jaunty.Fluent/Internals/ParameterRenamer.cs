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
            // Detect prefix from the parameter name itself (@ or $)
            var paramPrefix = name.Length > 0 && name[0] is '@' or '$' ? name[0].ToString() : "@";
            var baseName = name.TrimStart('@').TrimStart('$');
            var newName = $"{paramPrefix}{prefix}_{baseName}";

            // Replace in SQL - use word boundary to avoid partial matches
            var pattern = $@"{Regex.Escape(paramPrefix)}{Regex.Escape(baseName)}(?![a-zA-Z0-9_])";
            renamedSql = Regex.Replace(renamedSql, pattern, newName);

            renamedParams.Add(newName, value);
        }

        return (renamedSql, renamedParams);
    }
}
