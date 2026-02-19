using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Text;

namespace Jaunty.Internals.Parameters;

internal static class ParameterBinder
{
    private static readonly ConcurrentDictionary<(string, Type), CommandTemplate> TemplateCache = new();

    internal static void Bind(IDbCommand command, object parameters)
    {
        // Handle IDictionary<string, object?> directly (e.g., ExpandoObject, Dictionary)
        if (parameters is IDictionary<string, object?> dictParams)
        {
            BindFromDictionary(command, dictParams);
            return;
        }

        var type = parameters.GetType();
        var sql = command.CommandText;

        // Try get cached template
        if (TemplateCache.TryGetValue((sql, type), out var template))
        {
            template.Bind(command, parameters);
            return;
        }

        // Slow path: parse and bind, then cache if no collection expansion happened
        string[] sqlParamNames = SqlParameterParserCache.GetOrAdd(sql);
        var meta = ParameterCache.Get(type);

        // Build lookup from property names
        var propertyLookup = new Dictionary<string, ParameterMetadata>(meta.Length, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < meta.Length; i++)
        {
            propertyLookup[meta[i].Name] = meta[i];
        }

        // Check for collection parameters and expand SQL if needed
        var (expandedSql, expandedParams, expandedOriginalNames) = ExpandCollectionParameters(
            sql, sqlParamNames, propertyLookup, parameters);

        if (expandedSql is not null)
        {
            // Dynamic expansion: cannot cache this specific execution
            command.CommandText = expandedSql;
            BindDynamic(command, parameters, expandedSql, expandedParams, propertyLookup, meta, expandedOriginalNames);
            return;
        }

        // Standard query: build and cache template
        template = BuildTemplate(sql, sqlParamNames, propertyLookup, meta);
        TemplateCache.TryAdd((sql, type), template);
        template.Bind(command, parameters);
    }

    private static CommandTemplate BuildTemplate(string sql, string[] sqlParamNames, Dictionary<string, ParameterMetadata> propertyLookup, ParameterMetadata[] allMeta)
    {
        var boundNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<TemplateItem>();

        for (int i = 0; i < sqlParamNames.Length; i++)
        {
            string sqlName = sqlParamNames[i];
            if (!boundNames.Add(sqlName)) continue;

            if (propertyLookup.TryGetValue(sqlName, out var m))
            {
                items.Add(new TemplateItem(sqlName, m.Getter));
            }
            else
            {
                throw new ArgumentException($"No property found matching SQL parameter '@{sqlName}'.");
            }
        }

        // Validate unused
        var unused = new List<string>();
        for (int i = 0; i < allMeta.Length; i++)
        {
            if (!boundNames.Contains(allMeta[i].Name))
                unused.Add(allMeta[i].Name);
        }

        if (unused.Count > 0)
        {
            throw new ArgumentException($"Unused parameter properties: {string.Join(", ", unused)}. SQL contains no matching parameters.");
        }

        return new CommandTemplate(items.ToArray());
    }

    private static void BindDynamic(IDbCommand command, object parameters, string expandedSql, Dictionary<string, object?>? expandedParams, Dictionary<string, ParameterMetadata> propertyLookup, ParameterMetadata[] meta, HashSet<string>? expandedOriginalNames)
    {
        var sqlParamNames = SqlParameterParser.ExtractParameterNames(expandedSql);
        var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < sqlParamNames.Length; i++)
        {
            string sqlName = sqlParamNames[i];
            if (!bound.Add(sqlName)) continue;

            if (expandedParams is not null && expandedParams.TryGetValue(sqlName, out var expandedValue))
            {
                var p = command.CreateParameter();
                p.ParameterName = sqlName;
                p.Value = expandedValue ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            else if (propertyLookup.TryGetValue(sqlName, out var m))
            {
                var p = command.CreateParameter();
                p.ParameterName = sqlName;
                p.Value = m.Getter(parameters) ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            else
            {
                throw new ArgumentException($"No property found matching SQL parameter '@{sqlName}'.");
            }
        }
    }

    private class CommandTemplate(TemplateItem[] items)
    {
        private IDbDataParameter[]? _templates;

        public void Bind(IDbCommand command, object parameters)
        {
            _templates ??= CreateTemplates(command);

            var pCollection = command.Parameters;
            for (int i = 0; i < items.Length; i++)
            {
                ref readonly var item = ref items[i];
                var template = _templates[i];
                
                // Clone the template to avoid thread safety issues
                // and to prevent parameters from being bound to multiple commands
                var p = CloneParameter(command, template);
                p.Value = item.Getter(parameters) ?? DBNull.Value;
                pCollection.Add(p);
            }
        }

        private IDbDataParameter[] CreateTemplates(IDbCommand command)
        {
            var templates = new IDbDataParameter[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                var p = command.CreateParameter();
                p.ParameterName = items[i].Name;
                templates[i] = p;
            }
            return templates;
        }

        private static IDbDataParameter CloneParameter(IDbCommand command, IDbDataParameter template)
        {
            // If the provider supports ICloneable, use it (Fast Path)
            if (template is ICloneable cloneable)
            {
                return (IDbDataParameter)cloneable.Clone();
            }

            // Fallback: Create new and copy basic properties (Slow Path)
            var p = command.CreateParameter();
            p.ParameterName = template.ParameterName;
            p.DbType = template.DbType;
            p.Direction = template.Direction;
            return p;
        }
    }

    private readonly struct TemplateItem(string name, Func<object, object?> getter)
    {
        public readonly string Name = name;
        public readonly Func<object, object?> Getter = getter;
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

            if (IsCollection(value, out var items, out var count))
            {
                expansions ??= new List<CollectionExpansion>(2);
                expansions.Add(new CollectionExpansion(sqlName, items, count));
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
            if (expansion.Count == 0)
            {
                // Empty collection: use subquery that returns no rows
                replacement = "(SELECT NULL WHERE 1 = 0)";
            }
            else
            {
                // Pre-size StringBuilder for replacement: (@Name0, @Name1, ...)
                var sb = new StringBuilder(expansion.Count * (expansion.Name.Length + 5));
                sb.Append('(');
                int i = 0;
                foreach (var item in expansion.Items)
                {
                    if (i > 0) sb.Append(", ");
                    var expandedName = expansion.Name + i;
                    sb.Append('@').Append(expandedName);
                    expandedParams[expandedName] = item;
                    i++;
                }
                sb.Append(')');
                replacement = sb.ToString();
            }

            // Replace all occurrences (case-insensitive)
            result = ReplaceCaseInsensitive(result, "@" + expansion.Name, replacement);
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

    private static bool IsCollection(object value, out IEnumerable items, out int count)
    {
        items = null!;
        count = 0;

        // Exclude string and byte[] - they implement IEnumerable but aren't "collections" for our purposes
        if (value is string || value is byte[])
            return false;

        if (value is ICollection collection)
        {
            items = collection;
            count = collection.Count;
            return true;
        }

        if (value is IEnumerable enumerable)
        {
            items = enumerable;
            // Count manually if not ICollection
            int c = 0;
            foreach (var _ in enumerable) c++;
            count = c;
            return true;
        }

        return false;
    }

    private static bool IsParameterChar(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    private readonly struct CollectionExpansion(string name, IEnumerable items, int count)
    {
        public readonly string Name = name;
        public readonly IEnumerable Items = items;
        public readonly int Count = count;
    }

    private static void BindFromDictionary(IDbCommand command, IDictionary<string, object?> dictParams)
    {
        string[] sqlParamNames = SqlParameterParserCache.GetOrAdd(command.CommandText);
        var bound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < sqlParamNames.Length; i++)
        {
            string sqlName = sqlParamNames[i];
            if (!bound.Add(sqlName)) continue;

            if (dictParams.TryGetValue(sqlName, out var value))
            {
                var p = command.CreateParameter();
                p.ParameterName = sqlName;
                p.Value = value ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            else
            {
                throw new ArgumentException($"No value found in dictionary for SQL parameter '@{sqlName}'.");
            }
        }
    }
}
