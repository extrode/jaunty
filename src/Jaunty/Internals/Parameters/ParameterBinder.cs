using System.Collections;
using System.Data;
using System.Text;

namespace Jaunty.Internals.Parameters;

internal static class ParameterBinder
{
    internal static void Bind(IDbCommand command, object parameters)
    {
        string[] sqlParamNames = SqlParameterParserCache.GetOrAdd(command.CommandText);
        var meta = ParameterCache.Get(parameters.GetType());

        // Build lookup from property names
        var propertyLookup = new Dictionary<string, ParameterMetadata>(meta.Length, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < meta.Length; i++)
        {
            propertyLookup[meta[i].Name] = meta[i];
        }

        // Check for collection parameters and expand SQL if needed
        var (expandedSql, expandedParams, expandedOriginalNames) = ExpandCollectionParameters(
            command.CommandText, sqlParamNames, propertyLookup, parameters);

        if (expandedSql is not null)
        {
            command.CommandText = expandedSql;
            // Re-parse the expanded SQL for binding
            sqlParamNames = SqlParameterParser.ExtractParameterNames(expandedSql);
        }

        // Dedupe SQL params and validate all exist
        var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < sqlParamNames.Length; i++)
        {
            string sqlName = sqlParamNames[i];
            if (!bound.Add(sqlName)) continue;

            // Check expanded params first, then original properties
            if (expandedParams is not null && expandedParams.TryGetValue(sqlName, out var expandedValue))
            {
                IDbDataParameter p = command.CreateParameter();
                p.ParameterName = sqlName;
                p.Value = expandedValue ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            else if (propertyLookup.TryGetValue(sqlName, out var m))
            {
                IDbDataParameter p = command.CreateParameter();
                p.ParameterName = sqlName;
                p.Value = m.Getter(parameters) ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            else
            {
                throw new ArgumentException(
                    $"No property found matching SQL parameter '@{sqlName}'.");
            }
        }

        // Only check for truly unused properties (exclude collection params that were expanded)
        var unused = new List<string>();
        for (int i = 0; i < meta.Length; i++)
        {
            var name = meta[i].Name;
            if (!bound.Contains(name) &&
                (expandedOriginalNames is null || !expandedOriginalNames.Contains(name)))
            {
                unused.Add(name);
            }
        }

        if (unused.Count > 0)
        {
            throw new ArgumentException(
                $"Unused parameter properties: {string.Join(", ", unused)}. SQL contains no matching parameters.");
        }
    }

    private static (string? expandedSql, Dictionary<string, object?>? expandedParams, HashSet<string>? expandedOriginalNames)
        ExpandCollectionParameters(
            string sql,
            string[] sqlParamNames,
            Dictionary<string, ParameterMetadata> propertyLookup,
            object parameters)
    {
        // First pass: find collection parameters (deduplicated)
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<CollectionExpansion>? expansions = null;

        foreach (var sqlName in sqlParamNames)
        {
            if (!seen.Add(sqlName))
                continue; // Already processed

            if (!propertyLookup.TryGetValue(sqlName, out var meta))
                continue;

            var value = meta.Getter(parameters);
            if (value is null)
                continue;

            if (IsCollection(value, out var items))
            {
                expansions ??= new List<CollectionExpansion>(2);
                expansions.Add(new CollectionExpansion(sqlName, items));
            }
        }

        if (expansions is null)
            return (null, null, null);

        // Second pass: build replacements and expanded params
        var expandedParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var expandedOriginalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = sql;

        foreach (var expansion in expansions)
        {
            expandedOriginalNames.Add(expansion.Name);

            string replacement;
            if (expansion.Items.Count == 0)
            {
                // Empty collection: use subquery that returns no rows
                replacement = "(SELECT NULL WHERE 1 = 0)";
            }
            else
            {
                // Build (@Name0, @Name1, @Name2, ...)
                var parts = new string[expansion.Items.Count];
                for (int i = 0; i < expansion.Items.Count; i++)
                {
                    var expandedName = $"{expansion.Name}{i}";
                    parts[i] = $"@{expandedName}";
                    expandedParams[expandedName] = expansion.Items[i];
                }
                replacement = $"({string.Join(", ", parts)})";
            }

            // Replace all occurrences (case-insensitive)
            result = ReplaceCaseInsensitive(result, $"@{expansion.Name}", replacement);
        }

        return (result, expandedParams, expandedOriginalNames);
    }

    private static string ReplaceCaseInsensitive(string source, string oldValue, string newValue)
    {
        var sb = new StringBuilder();
        int currentIndex = 0;
        int foundIndex;

        while ((foundIndex = source.IndexOf(oldValue, currentIndex, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            // Check if this is a complete parameter (not part of a longer name)
            int endIndex = foundIndex + oldValue.Length;
            if (endIndex < source.Length && IsParameterChar(source[endIndex]))
            {
                // Part of a longer parameter name, skip
                sb.Append(source, currentIndex, foundIndex - currentIndex + oldValue.Length);
                currentIndex = endIndex;
                continue;
            }

            sb.Append(source, currentIndex, foundIndex - currentIndex);
            sb.Append(newValue);
            currentIndex = endIndex;
        }

        sb.Append(source, currentIndex, source.Length - currentIndex);
        return sb.ToString();
    }

    private static bool IsCollection(object value, out List<object?> items)
    {
        items = null!;

        // Exclude string and byte[] - they implement IEnumerable but aren't "collections" for our purposes
        if (value is string || value is byte[])
            return false;

        if (value is IEnumerable enumerable)
        {
            items = new List<object?>();
            foreach (var item in enumerable)
                items.Add(item);
            return true;
        }

        return false;
    }

    private static bool IsParameterChar(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    private readonly struct CollectionExpansion(string name, List<object?> items)
    {
        public readonly string Name = name;
        public readonly List<object?> Items = items;
    }
}
