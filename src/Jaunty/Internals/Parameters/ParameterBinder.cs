using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Text;

using Jaunty.Configuration;

namespace Jaunty.Internals.Parameters;

internal static class ParameterBinder
{
    private static readonly ConcurrentDictionary<(string Sql, Type ParamType, Type CommandType), CommandTemplate> TemplateCache = new();

    internal static void Bind(IDbCommand command, object parameters)
    {
        // Stored procedures/table-direct don't expose SQL parameter placeholders in CommandText,
        // so SQL-text parsing/validation is not applicable. Bind all provided values.
        if (command.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
        {
            if (parameters is IDictionary<string, object?> dict)
            {
                BindAllFromDictionary(command, dict);
                return;
            }

            BindAllFromObject(command, parameters);
            return;
        }

        // Handle IDictionary<string, object?> directly (e.g., ExpandoObject, Dictionary)
        if (parameters is IDictionary<string, object?> dictParams)
        {
            BindFromDictionary(command, dictParams);
            return;
        }

        // Handle scalar/value-type parameters (int, string, Guid, etc.)
        // These have no public instance properties to bind by name, so we bind positionally.
        if (IsScalarType(parameters.GetType()))
        {
            BindScalar(command, parameters);
            return;
        }

        var sql = command.CommandText;
        Type type = parameters.GetType();
        Type commandType = command.GetType();

        // Try get cached template
        if (TemplateCache.TryGetValue((sql, type, commandType), out CommandTemplate? template))
        {
            template.Bind(command, parameters);
            return;
        }

        // Slow path: parse and bind, then cache if no collection expansion happened
        string[] sqlParamNames = SqlParameterParserCache.GetOrAdd(sql);
        ParameterMetadata[] meta = ParameterCache.Get(type);

        // Build lookup from property names
        var propertyLookup = new Dictionary<string, ParameterMetadata>(meta.Length, CommonConstants.OrdinalIgnoreCase);
        for (int i = 0; i < meta.Length; i++)
        {
            propertyLookup[meta[i].Name] = meta[i];
        }

        // Check for collection parameters and expand SQL if needed
        (string? expandedSql, Dictionary<string, object?>? expandedParams, HashSet<string>? expandedOriginalNames) = ExpandCollectionParameters(sql, sqlParamNames, propertyLookup, parameters);

        if (expandedSql is not null)
        {
            // Dynamic expansion: cannot cache this specific execution
            command.CommandText = expandedSql;
            BindDynamic(command, parameters, expandedSql, expandedParams, propertyLookup, meta, expandedOriginalNames);
            return;
        }

        // Standard query: build and cache template
        template = BuildTemplate(type, sql, sqlParamNames, propertyLookup, meta);
        TemplateCache.TryAdd((sql, type, commandType), template);
        template.Bind(command, parameters);
    }

    private static CommandTemplate BuildTemplate(Type type, string sql, string[] sqlParamNames, Dictionary<string, ParameterMetadata> propertyLookup, ParameterMetadata[] allMeta)
    {
        var boundNames = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);
        var items = new List<TemplateItem>(JauntyConfig.ParameterParsingCapacity);

        for (int i = 0; i < sqlParamNames.Length; i++)
        {
            string sqlName = sqlParamNames[i];
            if (!boundNames.Add(sqlName)) continue;

            if (propertyLookup.TryGetValue(sqlName, out ParameterMetadata m))
            {
                items.Add(new TemplateItem(sqlName, m.Getter));
            }
            else
            {
                throw new ArgumentException($"No property found on type '{type.Name}' matching SQL parameter '@{sqlName}'. Available properties: {string.Join(", ", propertyLookup.Keys)}");
            }
        }

        // Validate unused
        var unused = new List<string>(JauntyConfig.ParameterParsingCapacity);
        for (int i = 0; i < allMeta.Length; i++)
        {
            if (!boundNames.Contains(allMeta[i].Name))
                unused.Add(allMeta[i].Name);
        }

        if (unused.Count > 0)
        {
            throw new ArgumentException($"Unused parameter properties on type '{type.Name}': {string.Join(", ", unused)}. SQL contains no matching parameters.");
        }

        return new CommandTemplate(items.ToArray());
    }

    private static void BindDynamic(IDbCommand command, object parameters, string expandedSql, Dictionary<string, object?>? expandedParams, Dictionary<string, ParameterMetadata> propertyLookup, ParameterMetadata[] meta, HashSet<string>? expandedOriginalNames)
    {
        Type type = parameters.GetType();
        var sqlParamNames = SqlParameterParser.ExtractParameterNames(expandedSql);
        var bound = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);

        for (int i = 0; i < sqlParamNames.Length; i++)
        {
            string sqlName = sqlParamNames[i];
            if (!bound.Add(sqlName)) continue;

            if (expandedParams is not null && expandedParams.TryGetValue(sqlName, out var expandedValue))
            {
                IDbDataParameter p = command.CreateParameter();
                p.ParameterName = sqlName;
                p.Value = expandedValue ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            else if (propertyLookup.TryGetValue(sqlName, out ParameterMetadata m))
            {
                IDbDataParameter p = command.CreateParameter();
                p.ParameterName = sqlName;
                p.Value = m.Getter(parameters) ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            else
            {
                throw new ArgumentException($"No property found on type '{type.Name}' matching SQL parameter '@{sqlName}'. Available properties: {string.Join(", ", propertyLookup.Keys)}");
            }
        }
    }

    private class CommandTemplate(TemplateItem[] items)
    {
        private IDbDataParameter[]? _templates;
        private Type? _templateCommandType;

        public void Bind(IDbCommand command, object parameters)
        {
            _templates ??= CreateTemplates(command);

            // Check if the command type matches the cached template's originating command type.
            // This prevents cross-provider bugs when multiple providers (e.g.,
            // System.Data.SQLite and Microsoft.Data.Sqlite) share the same SQL cache key.
            // Uses command type comparison (no allocation) instead of CreateParameter().GetType().
            bool sameProvider = command.GetType() == _templateCommandType;

            IDataParameterCollection pCollection = command.Parameters;
            for (int i = 0; i < items.Length; i++)
            {
                ref readonly TemplateItem item = ref items[i];
                IDbDataParameter template = _templates[i];

                // Clone the template to avoid thread safety issues
                // and to prevent parameters from being bound to multiple commands
                IDbDataParameter p = sameProvider ? CloneParameter(command, template) : CreateParameter(command, template);
                p.Value = item.Getter(parameters) ?? DBNull.Value;
                pCollection.Add(p);
            }
        }

        private IDbDataParameter[] CreateTemplates(IDbCommand command)
        {
            _templateCommandType = command.GetType();
            var templates = new IDbDataParameter[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                IDbDataParameter p = command.CreateParameter();
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

            return CreateParameter(command, template);
        }

        private static IDbDataParameter CreateParameter(IDbCommand command, IDbDataParameter template)
        {
            IDbDataParameter p = command.CreateParameter();
            p.ParameterName = template.ParameterName;
            // Intentionally do not copy DbType across providers; let provider infer from value.
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
        var seen = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);
        List<CollectionExpansion>? expansions = null;

        foreach (var sqlName in sqlParamNames)
        {
            if (!seen.Add(sqlName))
                continue; // Already processed

            if (!propertyLookup.TryGetValue(sqlName, out ParameterMetadata meta))
                continue;

            var value = meta.Getter(parameters);
            if (value is null)
                continue;

            if (IsCollection(value, out IEnumerable? items, out var count))
            {
                expansions ??= new List<CollectionExpansion>(2);
                expansions.Add(new CollectionExpansion(sqlName, items, count));
            }
        }

        if (expansions is null)
            return (null, null, null);

        // Second pass: build replacements and expanded params
        // Detect parameter prefix from the SQL (@ or $)
        var paramPrefix = DetectParameterPrefix(sql);
        var expandedParams = new Dictionary<string, object?>(CommonConstants.OrdinalIgnoreCase);
        var expandedOriginalNames = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);
        var result = sql;

        foreach (CollectionExpansion expansion in expansions)
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
                var sb = new StringBuilder(expansion.Count * (expansion.Name.Length + 5));
                sb.Append('(');
                int i = 0;
                foreach (var item in expansion.Items)
                {
                    if (i > 0) sb.Append(", ");
                    var expandedName = expansion.Name + i;
                    sb.Append(paramPrefix).Append(expandedName);
                    expandedParams[expandedName] = item;
                    i++;
                }
                sb.Append(')');
                replacement = sb.ToString();
            }

            // Replace all occurrences (case-insensitive)
            result = ReplaceCaseInsensitive(result, paramPrefix + expansion.Name, replacement);
        }

        return (result, expandedParams, expandedOriginalNames);
    }

    private static string DetectParameterPrefix(string sql)
    {
        // Detect parameter prefix from the SQL text (@ or $)
        for (int i = 0; i < sql.Length; i++)
        {
            var c = sql[i];
            if (c is '@' or '$')
            {
                // Check that next char is a valid parameter name start
                if (i + 1 < sql.Length && IsParameterChar(sql[i + 1]))
                    return c.ToString();
            }
        }
        return "@"; // default
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

    private static bool IsScalarType(Type type)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(string)
            || underlying == typeof(decimal)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(TimeSpan)
            || underlying == typeof(Guid)
            || underlying == typeof(byte[]);
    }

    private static void BindScalar(IDbCommand command, object value)
    {
        string[] sqlParamNames = SqlParameterParserCache.GetOrAdd(command.CommandText);
        var bound = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);

        for (int i = 0; i < sqlParamNames.Length; i++)
        {
            string sqlName = sqlParamNames[i];
            if (!bound.Add(sqlName)) continue;

            IDbDataParameter p = command.CreateParameter();
            p.ParameterName = sqlName;
            p.Value = value ?? DBNull.Value;
            command.Parameters.Add(p);
        }
    }

    private static void BindFromDictionary(IDbCommand command, IDictionary<string, object?> dictParams)
    {
        string[] sqlParamNames = SqlParameterParserCache.GetOrAdd(command.CommandText);
        var bound = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);

        for (int i = 0; i < sqlParamNames.Length; i++)
        {
            string sqlName = sqlParamNames[i];
            if (!bound.Add(sqlName)) continue;

            if (dictParams.TryGetValue(sqlName, out var value))
            {
                IDbDataParameter p = command.CreateParameter();
                p.ParameterName = sqlName;
                p.Value = value ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            else
            {
                throw new ArgumentException($"No value found in dictionary for SQL parameter '@{sqlName}'.", nameof(dictParams));
            }
        }
    }

    private static void BindAllFromObject(IDbCommand command, object parameters)
    {
        ParameterMetadata[] meta = ParameterCache.Get(parameters.GetType());
        for (int i = 0; i < meta.Length; i++)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = meta[i].Name;
            parameter.Value = meta[i].Getter(parameters) ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    private static void BindAllFromDictionary(IDbCommand command, IDictionary<string, object?> dictParams)
    {
        foreach (KeyValuePair<string, object?> kvp in dictParams)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = kvp.Key;
            parameter.Value = kvp.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}