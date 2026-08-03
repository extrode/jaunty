using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Text;

using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.StoredProcedure;
using Jaunty.TypeHandlers;

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

            // A scalar (int, string, Guid, etc.) has no named properties to bind, and unlike the
            // non-sproc path below there is no SQL text to parse to discover the target parameter
            // name, so silently binding zero parameters would execute the procedure with the
            // value dropped. Fail loudly instead, matching BindScalar's fail-loudly convention.
            if (IsScalarType(parameters.GetType()))
                throw new ArgumentException($"A scalar parameter value cannot be bound to a stored procedure or table-direct command by name. Pass an anonymous object, dictionary, or {nameof(SpParameters)} instead (e.g. new {{ CategoryId = 5 }}).", nameof(parameters));

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
        // AUD-R26: this comment said "so we bind positionally", which is not what BindScalar does and
        // was part of the same false claim the four Read files carried. There is no position to bind
        // to - a scalar has no properties, so BindScalar takes the target name from the SQL text and
        // throws when the SQL names more than one distinct parameter.
        if (IsScalarType(parameters.GetType()))
        {
            BindScalar(command, parameters);
            return;
        }

        string sql = command.CommandText;
        Type type = parameters.GetType();
        Type commandType = command.GetType();

        // Try get cached template
        if (TemplateCache.TryGetValue((sql, type, commandType), out CommandTemplate? template))
        {
            template!.Bind(command, parameters);
            return;
        }

        // Slow path: parse and bind, then cache if no collection expansion happened
        bool backslashEscapes = UsesBackslashEscapes(command.Connection);
        string[] sqlParamNames = SqlParameterParserCache.GetOrAdd(sql, backslashEscapes);
        ParameterMetadata[] meta = ParameterCache.Get(type);

        // Build lookup from property names
        var propertyLookup = new Dictionary<string, ParameterMetadata>(meta.Length, CommonConstants.OrdinalIgnoreCase);
        for (int i = 0; i < meta.Length; i++)
        {
            propertyLookup[meta[i].Name] = meta[i];
        }

        // Check for collection parameters and expand SQL if needed
        (string? expandedSql, Dictionary<string, ExpandedParameterValue>? expandedParams, HashSet<string>? expandedOriginalNames) = ExpandCollectionParameters(sql, sqlParamNames, propertyLookup, parameters, command.Connection);

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
            BindDynamic(command, parameters, sql, expandedParams: null, propertyLookup, meta, expandedOriginalNames: null, preParsedSqlParamNames: sqlParamNames);
            return;
        }

        // Standard query: build and cache template
        template = BuildTemplate(type, sql, sqlParamNames, propertyLookup, meta);
        TemplateCache.TryAdd((sql, type, commandType), template);
        template.Bind(command, parameters);
    }

    /// <summary>
    /// Re-binds <paramref name="parameters"/> onto a command whose parameter collection this binder
    /// already populated for the same SQL and the same parameter type, without discarding and
    /// recreating the provider parameter objects.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the values were updated in place; <see langword="false"/> when
    /// this shape has no cached template - a dictionary, a scalar, a collection-typed property, or
    /// SQL that needed IN-clause expansion - in which case the caller must
    /// <c>Parameters.Clear()</c> and call <see cref="Bind"/>.
    /// </returns>
    /// <remarks>
    /// AUD-R26, for <c>ExecuteBatch</c>. Deliberately conservative: it never builds or caches a
    /// template, so a caller that gets <see langword="false"/> is exactly where it was before, and
    /// no shape that <see cref="Bind"/> routes through the dynamic path is affected. It is the
    /// caller's job to establish that the parameter object's runtime type has not changed between
    /// calls - the templates deliberately do not carry a <c>DbType</c> (the provider infers it from
    /// the value), so reusing parameter objects across differently-typed values is not something
    /// this method can make safe on its own.
    /// </remarks>
    internal static bool TryRebind(IDbCommand command, object parameters)
    {
        if (command.CommandType is CommandType.StoredProcedure or CommandType.TableDirect)
            return false;

        if (parameters is IDictionary<string, object?> || IsScalarType(parameters.GetType()))
            return false;

        return TemplateCache.TryGetValue((command.CommandText, parameters.GetType(), command.GetType()), out CommandTemplate? template)
            && template!.TryRebind(command, parameters);
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

    // preParsedSqlParamNames: the already-extracted parameter names for expandedSql, when the caller
    // has them. Only ever passed for the un-expanded case - genuinely expanded SQL is a distinct
    // string per call and must be re-parsed.
    private static void BindDynamic(IDbCommand command, object parameters, string expandedSql, Dictionary<string, ExpandedParameterValue>? expandedParams, Dictionary<string, ParameterMetadata> propertyLookup, ParameterMetadata[] meta, HashSet<string>? expandedOriginalNames, string[]? preParsedSqlParamNames = null)
    {
        Type type = parameters.GetType();

        // AUD-R25: re-parsing is right for genuinely expanded SQL - each expansion is a distinct
        // string that must not be cached - but Bind also routes the *un-expanded* case here, when
        // the parameters type merely has a collection-typed property whose value happened to be
        // null. That SQL was already parsed through SqlParameterParserCache two lines earlier and
        // the result discarded, so every such call re-tokenized the whole statement on the hot path.
        string[] sqlParamNames = preParsedSqlParamNames ?? SqlParameterParser.ExtractParameterNames(expandedSql, UsesBackslashEscapes(command.Connection));
        var bound = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);

        for (int i = 0; i < sqlParamNames.Length; i++)
        {
            string sqlName = sqlParamNames[i];
            if (!bound.Add(sqlName)) continue;

            if (expandedParams is not null && expandedParams.TryGetValue(sqlName, out var expandedValue))
            {
                IDbDataParameter p = command.CreateParameter();
                p.ParameterName = sqlName;
                p.Value = ApplyTypeHandlerIfNeeded(expandedValue.Value, expandedValue.Property) ?? DBNull.Value;
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

        public void Bind(IDbCommand command, object parameters)
        {
            // TemplateCache keys on (Sql, ParamType, command.GetType()), so a CommandTemplate
            // instance is only ever Bind()-bound to commands of the exact provider type it was
            // created with - always clone rather than re-derive a provider-type check per call.
            //
            // AUD-R25: this was a plain "_templates ??= CreateTemplates(command);". A CommandTemplate
            // lives in the static TemplateCache and is handed to any thread binding the same
            // (sql, paramType, commandType), so that lazy init is a data race. The reference was
            // published with no release barrier, so under a weak memory model - arm64, both a
            // supported target and this project's dev machine - another thread could observe a
            // non-null _templates whose element writes were not yet visible and dereference a null
            // element in CloneParameter, giving an intermittent NullReferenceException deep inside
            // parameter binding. Interlocked.CompareExchange publishes with a full fence and lets
            // only one array win; Volatile.Read pairs with it on the fast path. Two threads racing
            // the first bind may each build an array, but only the published one is ever read, and
            // the loser's provider parameter objects are simply discarded.
            IDbDataParameter[]? templates = Volatile.Read(ref _templates);
            if (templates is null)
            {
                IDbDataParameter[] created = CreateTemplates(command);
                templates = Interlocked.CompareExchange(ref _templates, created, null) ?? created;
            }

            IDataParameterCollection pCollection = command.Parameters;
            for (int i = 0; i < items.Length; i++)
            {
                ref readonly TemplateItem item = ref items[i];
                IDbDataParameter template = templates[i];

                // Clone the template to avoid thread safety issues
                // and to prevent parameters from being bound to multiple commands
                IDbDataParameter p = CloneParameter(command, template);
                p.Value = ApplyTypeHandlerIfNeeded(item.Getter(parameters), item.Property) ?? DBNull.Value;
                pCollection.Add(p);
            }
        }

        /// <summary>
        /// Updates the values of a parameter collection this template already populated, instead of
        /// tearing it down and rebuilding it. Returns <see langword="false"/> when the collection is
        /// not one this template produced, in which case the caller must fall back to
        /// <see cref="Bind"/>.
        /// </summary>
        /// <remarks>
        /// AUD-R26. This is what <c>ExecuteBatch</c>'s <c>Prepare()</c> comment already claimed the
        /// code did - "mirroring BulkInsertLoop" - while the loop actually called
        /// <c>Parameters.Clear()</c> and a full rebind for every set, so <c>Prepare()</c> was being
        /// called on a command whose parameter collection was then torn down and rebuilt on every
        /// subsequent iteration. <c>BulkInsertLoop</c> does the opposite, and that is the point of
        /// it: bind once, then set <c>.Value</c> in place.
        /// </remarks>
        public bool TryRebind(IDbCommand command, object parameters)
        {
            IDataParameterCollection pCollection = command.Parameters;

            if (pCollection.Count != items.Length)
                return false;

            for (int i = 0; i < items.Length; i++)
            {
                if (pCollection[i] is not IDbDataParameter p)
                    return false;

                ref readonly TemplateItem item = ref items[i];

                // Position, not name. The collection was produced by this same template in this same
                // order, and the caller has already established that the parameter object's runtime
                // type is unchanged - so item i is the parameter for item i.
                if (!string.Equals(p.ParameterName, item.Name, StringComparison.Ordinal))
                    return false;

                p.Value = ApplyTypeHandlerIfNeeded(item.Getter(parameters), item.Property) ?? DBNull.Value;
            }

            return true;
        }

        private IDbDataParameter[] CreateTemplates(IDbCommand command)
        {
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
            return template is ICloneable cloneable ? (IDbDataParameter)cloneable.Clone() : CreateParameter(command, template);
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

    /// <summary>
    /// Whether the connection's engine treats a backslash as an escape inside a string literal
    /// (AUD-R34-014). MySQL and MariaDB do by default; every other engine does not, and applying
    /// the rule there would swallow the terminator of a literal ending in a backslash. Resolved by
    /// concrete dialect type rather than an <c>ISqlDialect</c> member so no public interface gains
    /// a member every third-party implementation would have to add.
    /// </summary>
    private static bool UsesBackslashEscapes(IDbConnection? connection)
        => connection is not null && SqlDialectFactory.Unwrap(SqlDialectFactory.GetDialect(connection)) is MySqlDialect;

    private static (string? expandedSql, Dictionary<string, ExpandedParameterValue>? expandedParams, HashSet<string>? expandedOriginalNames)
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
            {
                // AUD-R35-012. A null collection used to be skipped outright, so the placeholder
                // was left unrewritten and `... WHERE Id IN @Ids` reached the provider verbatim -
                // a syntax error naming neither Jaunty nor Ids. The empty collection two branches
                // down is handled with care (an empty-set subquery), and null and empty are the
                // two shapes a caller reaches by the same accident: a `List<int>?` left unset.
                // They now behave identically. The declared type is what decides, since there is
                // no value to inspect; IsCollectionType excludes string and byte[], so a null
                // string or blob still binds as a DBNull scalar.
                if (meta.Property is not null && IsCollectionType(meta.Property.PropertyType))
                {
                    expansions ??= new List<CollectionExpansion>(2);
                    expansions.Add(new CollectionExpansion(sqlName, Array.Empty<object?>(), 0, meta.Property));
                }

                continue;
            }

            if (IsCollection(value, out IEnumerable? items, out var count))
            {
                expansions ??= new List<CollectionExpansion>(2);
                expansions.Add(new CollectionExpansion(sqlName, items, count, meta.Property));
                totalExpandedCount += count;
            }
        }

        if (expansions is null)
            return (null, null, null);

        // Fail fast with a clear error instead of letting the provider reject SQL that
        // exceeds its parameter limit with an opaque driver-level exception.
        ISqlDialect? knownDialect = null;
        if (connection is not null)
        {
            ISqlDialect dialect = knownDialect = SqlDialectFactory.GetDialect(connection);
            // R16: totalExpandedCount alone undercounts - it ignores the non-collection scalar
            // placeholders in the same statement (seen.Count includes both), so a statement could
            // still exceed the provider's true parameter maximum while passing a collection-only
            // check.
            int totalParameterCount = totalExpandedCount + (seen.Count - expansions.Count);

            // AUD-R26: wording and threshold now live in ParameterCeiling, so the three Fluent
            // routes that expand collections themselves raise the same error rather than none.
            ParameterCeiling.EnsureWithinLimit(
                totalParameterCount, dialect, ParameterCeiling.Describe(connection, dialect));
        }

        // Second pass: build replacements and expanded params
        // Detect parameter prefix from the SQL (@ or $)
        bool backslashEscapes = knownDialect is not null && SqlDialectFactory.Unwrap(knownDialect) is MySqlDialect;
        var paramPrefix = DetectParameterPrefix(sql, backslashEscapes);
        var expandedParams = new Dictionary<string, ExpandedParameterValue>(CommonConstants.OrdinalIgnoreCase);
        var expandedOriginalNames = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);
        var replacements = new Dictionary<string, string>(CommonConstants.OrdinalIgnoreCase);

        // AUD-R34-013: minted placeholder names used to be `expansion.Name + i` with no check that
        // the name was free, so `new { Ids = new[] { 1, 2 }, Ids0 = 5 }` against
        // `... a IN @Ids AND b = @Ids0` rewrote to `IN (@Ids0, @Ids1) AND b = @Ids0` and the genuine
        // @Ids0 bound the collection's first element - BindDynamic consults the expanded names
        // before the property lookup. Reserve every name the SQL or the parameters object already
        // uses, and lengthen the mint prefix until it clears them all.
        var reserved = new HashSet<string>(seen, CommonConstants.OrdinalIgnoreCase);
        foreach (string propertyName in propertyLookup.Keys)
            reserved.Add(propertyName);

        foreach (CollectionExpansion expansion in expansions)
        {
            expandedOriginalNames.Add(expansion.Name);

            string replacement;
            if (expansion.Count == 0)
            {
                // Empty collection: a subquery that returns no rows, so IN matches nothing and
                // NOT IN matches everything. MySQL/MariaDB reject a FROM-less WHERE
                // (ER_NO_TABLES_USED), so that dialect gets the same subquery over DUAL.
                // Unwrap: with native bulk copy enabled the resolved dialect is the
                // MySqlDialectWithBulkCopy wrapper, not a MySqlDialect subtype.
                replacement = knownDialect is not null && SqlDialectFactory.Unwrap(knownDialect) is MySqlDialect
                    ? "(SELECT NULL FROM DUAL WHERE 1 = 0)"
                    : "(SELECT NULL WHERE 1 = 0)";
            }
            else
            {
                string mintPrefix = FreeMintPrefix(expansion.Name, expansion.Count, reserved);

                var sb = new StringBuilder(expansion.Count * (mintPrefix.Length + 5));
                sb.Append('(');
                int i = 0;
                foreach (var item in expansion.Items)
                {
                    if (i > 0) sb.Append(", ");
                    var expandedName = mintPrefix + i;
                    reserved.Add(expandedName);
                    sb.Append(paramPrefix).Append(expandedName);
                    expandedParams[expandedName] = new ExpandedParameterValue(item, expansion.Property);
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
        var result = ReplaceParametersLiteralAware(sql, paramPrefix[0], replacements, backslashEscapes);

        return (result, expandedParams, expandedOriginalNames);
    }

    /// <summary>
    /// The shortest prefix starting from <paramref name="name"/> for which none of
    /// <c>prefix + 0 .. prefix + (count - 1)</c> is already taken (AUD-R34-013). Underscores are
    /// appended one at a time; <paramref name="reserved"/> is finite, so this terminates, and the
    /// result is a legal parameter name because an underscore is a parameter character.
    /// </summary>
    private static string FreeMintPrefix(string name, int count, HashSet<string> reserved)
    {
        string prefix = name;

        while (true)
        {
            bool clear = true;
            for (int i = 0; i < count; i++)
            {
                if (reserved.Contains(prefix + i))
                {
                    clear = false;
                    break;
                }
            }

            if (clear)
                return prefix;

            prefix += "_";
        }
    }

    // Literal/comment-aware prefix detection. Walks the SQL using the same tokenization rules as
    // ReplaceParametersLiteralAware (skipping string literals, quoted identifiers, comments,
    // dollar-quoted strings, and @@ system variables) so a '$'/'@' inside a literal or comment
    // earlier in the SQL than the real placeholders is never mistaken for the parameter prefix.
    // Internal rather than private so AUD-R26's sigil-position rule can be tested at this site
    // directly; the precedent is AUD-R9-011, which promoted PostgreSqlSchemaReader's SQL consts for
    // the same reason. ParameterBinder is itself internal, so this widens nothing publicly.
    internal static string DetectParameterPrefix(string sql, bool backslashEscapes = false)
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

            // AUD-R34-012: the backtick was missing here while SqlParameterParser's twin walkers
            // already had it (AUD-R31-001), so only two of the four sigil walkers were fixed. A
            // backtick fell through to the default arm and an apostrophe inside `it's` then opened
            // a phantom string literal that ran to the end of the statement.
            if (c is '\'' or '"' or '[' or '`')
            {
                char terminator = c == '[' ? ']' : c;
                // AUD-R34-014: string literals only, and MySQL/MariaDB only.
                bool escapes = backslashEscapes && c is '\'' or '"';
                i++;
                while (i < len)
                {
                    if (escapes && sql[i] == '\\' && i + 1 < len) { i += 2; continue; }

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

            if (c is '@' or '$' && !SqlParameterParser.IsSigilInsideIdentifier(sql, i))
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
    private static string ReplaceParametersLiteralAware(string sql, char prefix, Dictionary<string, string> replacements, bool backslashEscapes)
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

            // String literal or quoted identifier: copy verbatim (handles doubled-quote escapes).
            // AUD-R34-012: the backtick arm was missing here too - see DetectParameterPrefix.
            if (c is '\'' or '"' or '[' or '`')
            {
                char terminator = c == '[' ? ']' : c;
                // AUD-R34-014: string literals only, and MySQL/MariaDB only.
                bool escapes = backslashEscapes && c is '\'' or '"';
                int start = i;
                i++;
                while (i < len)
                {
                    if (escapes && sql[i] == '\\' && i + 1 < len)
                    {
                        i += 2;
                        continue;
                    }

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

            // Genuine parameter placeholder - unless the sigil is inside an identifier.
            if (c == prefix && !SqlParameterParser.IsSigilInsideIdentifier(sql, i))
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

    // AUD-R26: was a byte-identical private copy of SqlParameterParser's. Forwarded rather than
    // duplicated, because the finding's requirement was that all four sigil sites agree, and two
    // copies of the rule is how they stop agreeing.
    private static bool IsParameterChar(char c) => SqlParameterParser.IsParameterChar(c);

    private readonly struct CollectionExpansion(string name, IEnumerable items, int count, PropertyInfo? property)
    {
        public readonly string Name = name;
        public readonly IEnumerable Items = items;
        public readonly int Count = count;
        public readonly PropertyInfo? Property = property;
    }

    // Carries the source property alongside each expanded IN-clause value so ApplyTypeHandlerIfNeeded
    // can still resolve a per-property [EnumStorage] override for collection-expanded parameters.
    private readonly struct ExpandedParameterValue(object? value, PropertyInfo? property)
    {
        public readonly object? Value = value;
        public readonly PropertyInfo? Property = property;
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
        string[] sqlParamNames = SqlParameterParserCache.GetOrAdd(command.CommandText, UsesBackslashEscapes(command.Connection));
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

        // Mirror BuildTemplate/BindFromDictionary's strictness: fail fast when the caller passed a
        // value but the SQL contains no matching placeholder, instead of silently dropping it.
        if (bound.Count == 0)
        {
            throw new ArgumentException("Unused scalar parameter value. SQL contains no matching parameters.", nameof(value));
        }

        foreach (string sqlName in bound)
        {
            IDbDataParameter p = command.CreateParameter();
            p.ParameterName = sqlName;
            p.Value = ApplyTypeHandlerIfNeeded(value, propertyInfo: null) ?? DBNull.Value;
            command.Parameters.Add(p);
        }
    }

    /// <summary>
    /// Binds an <see cref="IDictionary{TKey,TValue}"/> parameter set, matching keys to SQL parameter
    /// names case-insensitively.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26. This used to look keys up with a bare <c>dictParams.TryGetValue</c>, which uses
    /// whatever comparer the caller happened to construct the dictionary with, while the object/POCO
    /// path builds its own <c>OrdinalIgnoreCase</c> lookup. So the two documented-equivalent
    /// parameter forms disagreed, and the difference was invisible at the call site. Measured against
    /// Microsoft.Data.Sqlite for <c>... WHERE id = @Id</c>:
    /// </para>
    /// <code>
    /// new { id = 5 }                                                    -> OK
    /// new Dictionary&lt;string, object?&gt;                  { ["id"] = 5 } -> ArgumentException
    /// new Dictionary&lt;string, object?&gt;(OrdinalIgnoreCase){ ["id"] = 5 } -> OK
    /// </code>
    /// <para>
    /// A plain <c>Dictionary&lt;string, object?&gt;</c> - the form every example produces by default,
    /// and what <see cref="System.Dynamic.ExpandoObject"/> presents - is case-sensitive, and nothing
    /// in the XML docs mentioned it.
    /// </para>
    /// <para>
    /// The caller's own comparer is still tried first, so a dictionary that already matches costs
    /// nothing extra; the case-insensitive index is built only when a lookup misses, and only once.
    /// </para>
    /// <para>
    /// The <c>paramName</c> on the exceptions here is the literal <c>"parameters"</c>, not
    /// <c>nameof(dictParams)</c>: every public overload that reaches this method names the argument
    /// <c>parameters</c>, so the old <c>nameof</c> reported an internal name that appears nowhere in
    /// the caller's code.
    /// </para>
    /// </remarks>
    private static void BindFromDictionary(IDbCommand command, IDictionary<string, object?> dictParams)
    {
        string[] sqlParamNames = SqlParameterParserCache.GetOrAdd(command.CommandText, UsesBackslashEscapes(command.Connection));
        var bound = new HashSet<string>(CommonConstants.OrdinalIgnoreCase);

        Dictionary<string, object?>? caseInsensitive = null;

        for (int i = 0; i < sqlParamNames.Length; i++)
        {
            string sqlName = sqlParamNames[i];
            if (!bound.Add(sqlName)) continue;

            // The caller's comparer first: an exact hit is the common case and must not pay for the
            // fallback index.
            if (!dictParams.TryGetValue(sqlName, out object? value))
            {
                caseInsensitive ??= BuildCaseInsensitiveIndex(dictParams);

                if (!caseInsensitive.TryGetValue(sqlName, out value))
                {
                    throw new ArgumentException($"No value found in dictionary for SQL parameter '@{sqlName}'.", "parameters");
                }
            }

            IDbDataParameter p = command.CreateParameter();
            p.ParameterName = sqlName;
            p.Value = ApplyTypeHandlerIfNeeded(value, propertyInfo: null) ?? DBNull.Value;
            command.Parameters.Add(p);
        }

        // Validate unused, mirroring BuildTemplate's strictness for object-based binding: fail
        // fast on a dictionary key the SQL never references, instead of silently ignoring it.
        //
        // AUD-R26, secondary: `bound` is OrdinalIgnoreCase while the keys come from the caller's
        // dictionary, so a genuinely unused differently-cased duplicate ("Id" bound, "ID" unused)
        // was silently accepted while an unused key of any other spelling threw. It is no longer
        // reachable - BuildCaseInsensitiveIndex rejects the duplicate outright, because with
        // case-insensitive matching there is no answer to which of the two the caller meant.
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
                throw new ArgumentException($"Unused parameter keys in dictionary: {string.Join(", ", unused)}. SQL contains no matching parameters.", "parameters");
            }
        }
    }

    /// <summary>
    /// Re-indexes a caller's dictionary under <c>OrdinalIgnoreCase</c>, rejecting keys that differ
    /// only in case.
    /// </summary>
    /// <remarks>
    /// A case-sensitive dictionary can legitimately hold both <c>"Id"</c> and <c>"ID"</c>. Under
    /// case-insensitive matching there is no answer to which one <c>@Id</c> meant, and silently
    /// picking whichever enumerated first is precisely the class of silent wrong-value bug this
    /// round has been closing elsewhere. Say so instead.
    /// </remarks>
    private static Dictionary<string, object?> BuildCaseInsensitiveIndex(IDictionary<string, object?> dictParams)
    {
        var index = new Dictionary<string, object?>(dictParams.Count, CommonConstants.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, object?> entry in dictParams)
        {
#if NET8_0_OR_GREATER
            if (!index.TryAdd(entry.Key, entry.Value))
#else
            if (index.ContainsKey(entry.Key))
#endif
            {
                throw new ArgumentException(
                    $"Parameter dictionary contains keys differing only in case ('{entry.Key}'). Parameter names are matched case-insensitively, so this is ambiguous; use one spelling.",
                    "parameters");
            }
#if !NET8_0_OR_GREATER
            index[entry.Key] = entry.Value;
#endif
        }

        return index;
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

    internal static object? ApplyTypeHandlerIfNeeded(object? value, PropertyInfo? propertyInfo, EnumStorage? enumStorageOverride = null)
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
            EnumStorage storage = GetEnumStorage(propertyInfo, enumStorageOverride);
            if (storage == EnumStorage.String)
            {
                return value.ToString();
            }
        }
        // AUD-R25: an "else if (valueType.IsGenericType)" branch here, commented "Handle nullable
        // enums", was unreachable. valueType comes from value.GetType() above, and boxing a
        // SomeEnum? produces a box of SomeEnum - GetType() can never return Nullable<SomeEnum>.
        // Nullable enums are already handled by the IsEnum branch, so the branch covered nothing
        // while reading as though the two cases differed; its redundant "value != null" re-check
        // (already guaranteed by the guard at the top) reinforced that misreading.

        return value;
    }

    // AUD-R35: the override is checked before the reflection lookup because the source-generated
    // path has no PropertyInfo to reflect over - ColumnMetadata.Property is null there - so
    // without it every generated-path caller fell through to JauntyConfig.DefaultEnumStorage and
    // silently ignored a property-level [EnumStorage]. The two are never both populated: the
    // reflection path supplies the property, the generated path supplies the override.
    private static EnumStorage GetEnumStorage(PropertyInfo? property, EnumStorage? enumStorageOverride)
    {
        if (enumStorageOverride.HasValue)
        {
            return enumStorageOverride.Value;
        }

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