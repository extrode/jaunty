using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Text;
using System.Reflection;

using Jaunty.Configuration;
using Jaunty.TypeHandlers;
using Jaunty.Attributes;
using Jaunty.Dialects;

namespace Jaunty.Internals.Parameters;

internal static class ParameterBinder
{
    // Size-capped to prevent unbounded growth when callers embed literals instead of parameters
    // or generate SQL dynamically (each distinct SQL text would otherwise be a permanent key).
    private static readonly BoundedCache<(string Sql, Type ParamType, Type CommandType), CommandTemplate> TemplateCache = new();

    // Caches the EnumStorageAttribute reflection lookup per property so ApplyTypeHandlerIfNeeded
    // doesn't call PropertyInfo.GetCustomAttribute on every parameter bind. Bounded by the number
    // of distinct properties across parameter types used by the application (not user input), so
    // no size cap is needed here (unlike TemplateCache above).
    private static readonly ConcurrentDictionary<PropertyInfo, EnumStorageAttribute?> EnumStorageAttributeCache = new();

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
            template!.Bind(command, parameters);
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
        (string? expandedSql, Dictionary<string, object?>? expandedParams, HashSet<string>? expandedOriginalNames) = ExpandCollectionParameters(sql, sqlParamNames, propertyLookup, parameters, command.Connection);

        if (expandedSql is not null)
        {
            // Dynamic expansion: cannot cache this specific execution
            command.CommandText = expandedSql;
            BindDynamic(command, parameters, expandedSql, expandedParams, propertyLookup, meta, expandedOriginalNames);
            return;
        }

        // A collection-typed property may simply have been null on this call (nothing to expand),
        // even though the same (sql, type, commandType) key could be called again later with a
        // non-null collection requiring IN-clause expansion. Caching a plain scalar-binding
        // template here would permanently defeat that expansion, so route this shape through the
        // per-call dynamic binding path instead of caching.
        if (HasCollectionTypedProperty(meta))
        {
            BindDynamic(command, parameters, sql, expandedParams: null, propertyLookup, meta, expandedOriginalNames: null);
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
                items.Add(new TemplateItem(sqlName, m.Getter, m.Property));
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
                p.Value = ApplyTypeHandlerIfNeeded(expandedValue, propertyInfo: null) ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            else if (propertyLookup.TryGetValue(sqlName, out ParameterMetadata m))
            {
                IDbDataParameter p = command.CreateParameter();
                p.ParameterName = sqlName;
                p.Value = ApplyTypeHandlerIfNeeded(m.Getter(parameters), m.Property) ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            else
            {
                throw new ArgumentException($"No property found on type '{type.Name}' matching SQL parameter '@{sqlName}'. Available properties: {string.Join(", ", propertyLookup.Keys)}");
            }
        }

        // Validate unused. A property that was collection-expanded (e.g. Ids -> @Ids0, @Ids1, ...)
        // no longer appears verbatim in 'bound' (which reflects the expanded SQL's placeholder
        // names), so it's checked against 'expandedOriginalNames' instead of being misreported.
        var unused = new List<string>(JauntyConfig.ParameterParsingCapacity);
        for (int i = 0; i < meta.Length; i++)
        {
            string name = meta[i].Name;
            if (bound.Contains(name)) continue;
            if (expandedOriginalNames is not null && expandedOriginalNames.Contains(name)) continue;
            unused.Add(name);
        }

        if (unused.Count > 0)
        {
            throw new ArgumentException($"Unused parameter properties on type '{type.Name}': {string.Join(", ", unused)}. SQL contains no matching parameters.");
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
                p.Value = ApplyTypeHandlerIfNeeded(item.Getter(parameters), item.Property) ?? DBNull.Value;
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

    private readonly struct TemplateItem(string name, Func<object, object?> getter, PropertyInfo? property)
    {
        public readonly string Name = name;
        public readonly Func<object, object?> Getter = getter;
        public readonly PropertyInfo? Property = property;
    }

    private static (string? expandedSql, Dictionary<string, object?>? expandedParams, HashSet<string>? expandedOriginalNames)
        ExpandCollectionParameters(
            string sql,
            string[] sqlParamNames,
            Dictionary<string, ParameterMetadata> propertyLookup,
            object parameters,
            IDbConnection? connection)
    {
        // First pass: find collection parameters (deduplicated)
        var seen = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);
        List<CollectionExpansion>? expansions = null;
        int totalExpandedCount = 0;

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
                totalExpandedCount += count;
            }
        }

        if (expansions is null)
            return (null, null, null);

        // Fail fast with a clear error instead of letting the provider reject SQL that
        // exceeds its parameter limit with an opaque driver-level exception.
        if (connection is not null)
        {
            ISqlDialect dialect = SqlDialectFactory.GetDialect(connection);
            if (totalExpandedCount > dialect.MaxParametersPerStatement)
            {
                throw new InvalidOperationException(
                    $"Collection parameter expansion produces {totalExpandedCount} parameters, exceeding the " +
                    $"{connection.GetType().Name} provider's maximum of {dialect.MaxParametersPerStatement} parameters " +
                    "per statement. Consider batching the query into smaller chunks.");
            }
        }

        // Second pass: build replacements and expanded params
        // Detect parameter prefix from the SQL (@ or $)
        var paramPrefix = DetectParameterPrefix(sql);
        var expandedParams = new Dictionary<string, object?>(CommonConstants.OrdinalIgnoreCase);
        var expandedOriginalNames = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);
        var replacements = new Dictionary<string, string>(CommonConstants.OrdinalIgnoreCase);

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

            replacements[expansion.Name] = replacement;
        }

        // Rewrite the SQL in a single literal/comment-aware pass so that @Name-looking text inside
        // string literals, quoted identifiers, or comments is never mistaken for a real placeholder
        // (unlike a naive textual find/replace, which would corrupt such SQL).
        var result = ReplaceParametersLiteralAware(sql, paramPrefix[0], replacements);

        return (result, expandedParams, expandedOriginalNames);
    }

    // Literal/comment-aware prefix detection. Walks the SQL using the same tokenization rules as
    // ReplaceParametersLiteralAware (skipping string literals, quoted identifiers, comments,
    // dollar-quoted strings, and @@ system variables) so a '$'/'@' inside a literal or comment
    // earlier in the SQL than the real placeholders is never mistaken for the parameter prefix.
    private static string DetectParameterPrefix(string sql)
    {
        int len = sql.Length;
        int i = 0;

        while (i < len)
        {
            char c = sql[i];

            if (c == '-' && i + 1 < len && sql[i + 1] == '-')
            {
                i += 2;
                while (i < len && sql[i] is not ('\n' or '\r')) i++;
                continue;
            }

            if (c == '/' && i + 1 < len && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < len && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                i = i + 1 < len ? i + 2 : len;
                continue;
            }

            if (c is '\'' or '"' or '[')
            {
                char terminator = c == '[' ? ']' : c;
                i++;
                while (i < len)
                {
                    if (sql[i] == terminator)
                    {
                        if (i + 1 < len && sql[i + 1] == terminator) { i += 2; continue; }
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }

            if (c == '@' && i + 1 < len && sql[i + 1] == '@')
            {
                i += 2;
                while (i < len && IsParameterChar(sql[i])) i++;
                continue;
            }

            if (c == '$')
            {
                int dollarQuoteEnd = TrySkipDollarQuotedString(sql, i, len);
                if (dollarQuoteEnd >= 0)
                {
                    i = dollarQuoteEnd;
                    continue;
                }
            }

            if (c is '@' or '$')
            {
                // Check that next char is a valid parameter name start
                if (i + 1 < len && IsParameterChar(sql[i + 1]))
                    return c.ToString();
            }

            i++;
        }

        return "@"; // default
    }

    // A dollar-quote opening tag is '$' + zero-or-more identifier chars + '$' (e.g. "$$" or
    // "$tag$"); mirrors SqlParameterParser.TrySkipDollarQuotedClassic. Returns the index just
    // past the closing delimiter, or -1 if 'sql[dollarPos]' is not the start of a dollar-quote.
    private static int TrySkipDollarQuotedString(string sql, int dollarPos, int len)
    {
        int tagEnd = dollarPos + 1;
        while (tagEnd < len && IsParameterChar(sql[tagEnd]))
            tagEnd++;

        if (tagEnd >= len || sql[tagEnd] != '$')
            return -1;

        int delimLen = tagEnd + 1 - dollarPos;
        int searchFrom = tagEnd + 1;
        while (searchFrom + delimLen <= len)
        {
            if (string.CompareOrdinal(sql, searchFrom, sql, dollarPos, delimLen) == 0)
                return searchFrom + delimLen;
            searchFrom++;
        }

        return len;
    }

    // Literal/comment-aware placeholder rewrite. Walks the SQL using the same tokenization rules as
    // SqlParameterParser (skipping string literals, quoted identifiers, comments, and @@ system
    // variables) and replaces only genuine parameter placeholders whose name is in 'replacements'.
    private static string ReplaceParametersLiteralAware(string sql, char prefix, Dictionary<string, string> replacements)
    {
        var sb = new StringBuilder(sql.Length + 16);
        int i = 0;
        int len = sql.Length;

        while (i < len)
        {
            char c = sql[i];

            // Single-line comment: copy verbatim to end of line
            if (c == '-' && i + 1 < len && sql[i + 1] == '-')
            {
                int start = i;
                i += 2;
                while (i < len && sql[i] is not ('\n' or '\r')) i++;
                sb.Append(sql, start, i - start);
                continue;
            }

            // Block comment: copy verbatim through the closing */
            if (c == '/' && i + 1 < len && sql[i + 1] == '*')
            {
                int start = i;
                i += 2;
                while (i + 1 < len && !(sql[i] == '*' && sql[i + 1] == '/')) i++;
                i = i + 1 < len ? i + 2 : len;
                sb.Append(sql, start, i - start);
                continue;
            }

            // String literal or quoted identifier: copy verbatim (handles doubled-quote escapes)
            if (c is '\'' or '"' or '[')
            {
                char terminator = c == '[' ? ']' : c;
                int start = i;
                i++;
                while (i < len)
                {
                    if (sql[i] == terminator)
                    {
                        if (i + 1 < len && sql[i + 1] == terminator)
                        {
                            i += 2;
                            continue;
                        }
                        i++;
                        break;
                    }
                    i++;
                }
                sb.Append(sql, start, i - start);
                continue;
            }

            // SQL Server @@ system variable: copy verbatim, never a bindable parameter
            if (c == '@' && i + 1 < len && sql[i + 1] == '@')
            {
                int start = i;
                i += 2;
                while (i < len && IsParameterChar(sql[i])) i++;
                sb.Append(sql, start, i - start);
                continue;
            }

            // PostgreSQL/DuckDB dollar-quoted string ($$...$$ or $tag$...$tag$): copy verbatim,
            // never a bindable parameter.
            if (c == '$')
            {
                int dollarQuoteEnd = TrySkipDollarQuotedString(sql, i, len);
                if (dollarQuoteEnd >= 0)
                {
                    sb.Append(sql, i, dollarQuoteEnd - i);
                    i = dollarQuoteEnd;
                    continue;
                }
            }

            // Genuine parameter placeholder
            if (c == prefix)
            {
                int nameStart = i + 1;
                int j = nameStart;
                while (j < len && IsParameterChar(sql[j])) j++;
                if (j > nameStart && replacements.TryGetValue(sql.Substring(nameStart, j - nameStart), out string? replacement))
                {
                    sb.Append(replacement);
                    i = j;
                    continue;
                }
            }

            sb.Append(c);
            i++;
        }

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
            // A non-ICollection sequence (e.g. a yield-return iterator or a side-effecting source)
            // may be forward-only/one-shot. Enumerate exactly once, materializing into a list that
            // is used for both the count and the later value binding, so we never re-run the source.
            var materialized = new List<object?>();
            foreach (var item in enumerable)
            {
                materialized.Add(item);
            }
            items = materialized;
            count = materialized.Count;
            return true;
        }

        return false;
    }

    // Type-based collection check (mirrors IsCollection's string/byte[] exclusion), used to decide
    // whether a (sql, type, commandType) shape can ever need IN-clause expansion, independent of
    // whether this particular call's value happened to be null.
    private static bool HasCollectionTypedProperty(ParameterMetadata[] meta)
    {
        for (int i = 0; i < meta.Length; i++)
        {
            if (meta[i].Property is PropertyInfo property && IsCollectionType(property.PropertyType))
                return true;
        }

        return false;
    }

    private static bool IsCollectionType(Type type)
    {
        if (type == typeof(string) || type == typeof(byte[]))
            return false;

        return typeof(IEnumerable).IsAssignableFrom(type);
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
            || underlying == typeof(byte[])
#if NET8_0_OR_GREATER
            || underlying == typeof(DateOnly)
            || underlying == typeof(TimeOnly)
#endif
            ;
    }

    private static void BindScalar(IDbCommand command, object value)
    {
        string[] sqlParamNames = SqlParameterParserCache.GetOrAdd(command.CommandText);
        var bound = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);

        for (int i = 0; i < sqlParamNames.Length; i++)
        {
            bound.Add(sqlParamNames[i]);
        }

        // A single scalar value is ambiguous when the SQL references more than one distinct
        // parameter: it would silently bind the same value to all of them. Mirror the named-object
        // path's arity checking and require the caller to pass an object/dictionary instead.
        if (bound.Count > 1)
        {
            throw new ArgumentException(
                $"A single scalar parameter value cannot be bound to SQL containing {bound.Count} distinct parameters ({string.Join(", ", bound)}). Pass an object or dictionary with a value per parameter instead.",
                nameof(value));
        }

        foreach (string sqlName in bound)
        {
            IDbDataParameter p = command.CreateParameter();
            p.ParameterName = sqlName;
            p.Value = ApplyTypeHandlerIfNeeded(value, propertyInfo: null) ?? DBNull.Value;
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
                p.Value = ApplyTypeHandlerIfNeeded(value, propertyInfo: null) ?? DBNull.Value;
                command.Parameters.Add(p);
            }
            else
            {
                throw new ArgumentException($"No value found in dictionary for SQL parameter '@{sqlName}'.", nameof(dictParams));
            }
        }

        // Validate unused, mirroring BuildTemplate's strictness for object-based binding: fail
        // fast on a dictionary key the SQL never references, instead of silently ignoring it.
        if (dictParams.Count > bound.Count)
        {
            List<string>? unused = null;
            foreach (string key in dictParams.Keys)
            {
                if (!bound.Contains(key))
                    (unused ??= new List<string>()).Add(key);
            }

            if (unused is not null)
            {
                throw new ArgumentException($"Unused parameter keys in dictionary: {string.Join(", ", unused)}. SQL contains no matching parameters.", nameof(dictParams));
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
            parameter.Value = ApplyTypeHandlerIfNeeded(meta[i].Getter(parameters), meta[i].Property) ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    private static void BindAllFromDictionary(IDbCommand command, IDictionary<string, object?> dictParams)
    {
        foreach (KeyValuePair<string, object?> kvp in dictParams)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = kvp.Key;
            parameter.Value = ApplyTypeHandlerIfNeeded(kvp.Value, propertyInfo: null) ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    internal static object? ApplyTypeHandlerIfNeeded(object? value, PropertyInfo? propertyInfo)
    {
        if (value is null)
            return value;

        Type valueType = value.GetType();

        // Check if there's a registered type handler first
        if (TypeHandlerRegistry.HasHandlers && TypeHandlerRegistry.TryGetHandler(valueType, out ITypeHandler? handler) && handler is not null)
        {
            try
            {
                return handler.ToDbValue(value);
            }
            catch (Exception ex)
            {
                // Surface conversion failures rather than silently binding unconverted data.
                throw new InvalidOperationException(
                    $"Type handler '{handler.GetType().Name}' failed to convert a value of type '{valueType.Name}' to its database representation.",
                    ex);
            }
        }

        // Handle enums based on storage strategy
        if (valueType.IsEnum)
        {
            EnumStorage storage = GetEnumStorage(propertyInfo);
            if (storage == EnumStorage.String)
            {
                return value.ToString();
            }
        }
        else if (valueType.IsGenericType)
        {
            Type? underlyingType = Nullable.GetUnderlyingType(valueType);
            if (underlyingType?.IsEnum == true)
            {
                // Handle nullable enums
                EnumStorage storage = GetEnumStorage(propertyInfo);
                if (storage == EnumStorage.String && value != null)
                {
                    return value.ToString();
                }
            }
        }

        return value;
    }

    private static EnumStorage GetEnumStorage(PropertyInfo? property)
    {
        if (property is not null)
        {
            EnumStorageAttribute? enumAttr = EnumStorageAttributeCache.GetOrAdd(property, static p => p.GetCustomAttribute<EnumStorageAttribute>());
            if (enumAttr is not null)
            {
                return enumAttr.Storage;
            }
        }
        return JauntyConfig.DefaultEnumStorage;
    }

}