using System.Data;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Linq.Expressions;
using System.Reflection;

using Jaunty.Interfaces;
using Jaunty.Configuration;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Parameters;

namespace Jaunty.Internals.Write;

/// <summary>
/// Caches compiled binders for high-performance write operations.
/// </summary>
internal static class WriteParameterCache<T> where T : new()
{
    /// <summary>
    /// Derived from <typeparamref name="T"/>'s own interfaces rather than from configuration, so
    /// this genuinely is fixed for the lifetime of the process and is not part of
    /// <see cref="Bindings"/>. Rebuilding it on a configuration change would recompile an expression
    /// tree to reach the same answer.
    /// </summary>
    public static readonly Action<T, long>? IdSetter = CreateIdSetter();

    /// <summary>
    /// <typeparamref name="T"/>'s source-generated accessors, or <see langword="null"/> when
    /// <typeparamref name="T"/> is not source-generated.
    /// </summary>
    /// <remarks>
    /// Spec 009. Resolved once per closed generic - not once per binder, and not once per
    /// <see cref="Bindings"/> rebuild - so the <c>new T()</c> the cast needs is paid a single time.
    /// Declared above <see cref="_bindings"/> deliberately: static initialisers run in textual order
    /// and <see cref="Bindings.Build"/> reads this field.
    /// </remarks>
    private static readonly IGeneratedAccessors<T>? Accessors =
        typeof(IGeneratedAccessors<T>).IsAssignableFrom(typeof(T)) ? (IGeneratedAccessors<T>)new T() : null;

    private static volatile Bindings _bindings = Bindings.Build();

    public static Action<IDbCommand, T>? InsertBinder => Current().InsertBinder;
    public static Action<IDbCommand, T>? UpdateBinder => Current().UpdateBinder;
    public static Action<IDbCommand, T>? DeleteBinder => Current().DeleteBinder;

    public static Action<IDataParameterCollection, T>? InsertValueSetter => Current().InsertValueSetter;
    public static Action<IDataParameterCollection, T>? UpdateValueSetter => Current().UpdateValueSetter;
    public static Action<IDataParameterCollection, T>? DeleteValueSetter => Current().DeleteValueSetter;

    /// <summary>
    /// Everything this type derives from <see cref="JauntyConfig"/>, tagged with the configuration
    /// generation it was derived under.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26 (batch 4, medium/bug). These were <c>static readonly</c> fields assigned in a static
    /// constructor, which meant the binder resolvers were read exactly once per entity type per
    /// process. If that constructor ran while they were null - which <c>JauntyConfig.Reset()</c>
    /// guarantees, and which the documented NativeAOT startup workaround makes easy to hit by
    /// touching a write before calling <c>UseReflectionMapping()</c> - then <c>Insert&lt;T&gt;</c>,
    /// <c>Update&lt;T&gt;</c> and <c>Delete&lt;T&gt;</c> were broken for that <c>T</c> forever, and
    /// re-registering the resolver did nothing. Measured: three consecutive inserts after
    /// re-registration, all three failing with "No parameter binder found for type 'Item'" - whose
    /// advice, "ensure source generation or reflection extension is used", was actively misleading
    /// because the extension <em>was</em> loaded.
    /// </para>
    /// <para>
    /// The read path never had this: <c>DrDispatcher</c> consults
    /// <c>JauntyConfig.ReflectionMapperResolver</c> per resolution, so clearing it breaks reads and
    /// restoring it fixes them. Two halves of one configuration surface, one recoverable and one
    /// not. This makes the write half behave like the read half without giving up the caching -
    /// see <see cref="ConfigurationGeneration"/>.
    /// </para>
    /// </remarks>
    private sealed class Bindings
    {
        public int Generation { get; private set; }
        public Action<IDbCommand, T>? InsertBinder { get; private set; }
        public Action<IDbCommand, T>? UpdateBinder { get; private set; }
        public Action<IDbCommand, T>? DeleteBinder { get; private set; }
        public Action<IDataParameterCollection, T>? InsertValueSetter { get; private set; }
        public Action<IDataParameterCollection, T>? UpdateValueSetter { get; private set; }
        public Action<IDataParameterCollection, T>? DeleteValueSetter { get; private set; }

        public static Bindings Build()
        {
            // Read the generation before building, never after: see ConfigurationGeneration.Current.
            var bindings = new Bindings { Generation = ConfigurationGeneration.Current };

            // Spec 009: the generated binder comes from IGeneratedAccessors<T> when T is
            // source-generated - a delegate handed over directly, so BindInsert/BindUpdate/BindDelete
            // are statically referenced and survive trimming. TryGetGeneratedBinder's reflection is
            // now only for types the generator did not produce. Resolution order is unchanged:
            // generated, then reflection-by-name, then the JauntyConfig resolver.
            bindings.InsertBinder = Accessors?.InsertBinder ?? TryGetGeneratedBinder("BindInsert") ?? TryGetReflectionBinder(JauntyConfig.ReflectionInsertBinderResolver);
            bindings.UpdateBinder = Accessors?.UpdateBinder ?? TryGetGeneratedBinder("BindUpdate") ?? TryGetReflectionBinder(JauntyConfig.ReflectionUpdateBinderResolver);
            bindings.DeleteBinder = Accessors?.DeleteBinder ?? TryGetGeneratedBinder("BindDelete") ?? TryGetReflectionBinder(JauntyConfig.ReflectionDeleteBinderResolver);

            // Value setters for bulk operations: update values on existing parameters by index.
            // PrepareXxxParameters has already created provider-native parameters on the command;
            // the value setter just walks the collection and sets .Value for each matching param.
            // Metadata is resolved once here (rather than separately inside each Create*ValueSetter)
            // since ResolveMetadata() re-instantiates new T() and rebuilds EntityMetadata from scratch.
            EntityMetadata? metadata = ResolveMetadata();
            bindings.InsertValueSetter = bindings.InsertBinder != null ? CreateInsertValueSetter(metadata) : null;
            bindings.UpdateValueSetter = bindings.UpdateBinder != null ? CreateUpdateValueSetter(metadata) : null;
            bindings.DeleteValueSetter = bindings.DeleteBinder != null ? CreateDeleteValueSetter(metadata) : null;

            return bindings;
        }
    }

    /// <summary>
    /// The bindings for the configuration as it stands, rebuilding them if it has moved since they
    /// were built.
    /// </summary>
    /// <remarks>
    /// Two threads racing here both build and both publish; whichever writes last wins and both
    /// answers are equally valid, since each was built from the configuration it recorded. A build
    /// overtaken by a concurrent configuration change publishes a snapshot tagged with the older
    /// generation, so the next read rebuilds rather than keeping it - the race costs a wasted build,
    /// never a stale answer.
    /// </remarks>
    private static Bindings Current()
    {
        Bindings current = _bindings;
        if (current.Generation == ConfigurationGeneration.Current)
            return current;

        Bindings rebuilt = Bindings.Build();
        _bindings = rebuilt;
        return rebuilt;
    }

    private static Action<IDataParameterCollection, T>? CreateInsertValueSetter(EntityMetadata? metadata)
    {
        if (metadata == null) return null;

        IReadOnlyList<ColumnMetadata> columns = metadata.InsertColumns;
        var getters = new Func<T, object?>[columns.Count];
        var properties = new PropertyInfo?[columns.Count];
        var columnNames = new string[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            getters[i] = CreateTypedGetter(columns[i]);
            properties[i] = columns[i].Property;
            columnNames[i] = columns[i].ColumnName;
        }

        return CreateValueSetter(getters, properties, columnNames, "insert");
    }

    private static Action<IDataParameterCollection, T>? CreateUpdateValueSetter(EntityMetadata? metadata)
    {
        if (metadata == null) return null;

        IReadOnlyList<ColumnMetadata> updateColumns = metadata.UpdateColumns;
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;
        var getters = new Func<T, object?>[updateColumns.Count + primaryKeys.Count];
        var properties = new PropertyInfo?[updateColumns.Count + primaryKeys.Count];
        var columnNames = new string[updateColumns.Count + primaryKeys.Count];

        // Non-key columns first, then keys: the order PrepareUpdateParameters creates the
        // parameters in, and the order JauntyGenerator emits BindUpdate's assignments in.
        for (int i = 0; i < updateColumns.Count; i++)
        {
            getters[i] = CreateTypedGetter(updateColumns[i]);
            properties[i] = updateColumns[i].Property;
            columnNames[i] = updateColumns[i].ColumnName;
        }
        for (int i = 0; i < primaryKeys.Count; i++)
        {
            getters[updateColumns.Count + i] = CreateTypedGetter(primaryKeys[i]);
            properties[updateColumns.Count + i] = primaryKeys[i].Property;
            columnNames[updateColumns.Count + i] = primaryKeys[i].ColumnName;
        }

        return CreateValueSetter(getters, properties, columnNames, "update");
    }

    private static Action<IDataParameterCollection, T>? CreateDeleteValueSetter(EntityMetadata? metadata)
    {
        if (metadata == null) return null;

        IReadOnlyList<ColumnMetadata> deleteColumns = metadata.DeleteColumns;
        var getters = new Func<T, object?>[deleteColumns.Count];
        var properties = new PropertyInfo?[deleteColumns.Count];
        var columnNames = new string[deleteColumns.Count];
        for (int i = 0; i < deleteColumns.Count; i++)
        {
            getters[i] = CreateTypedGetter(deleteColumns[i]);
            properties[i] = deleteColumns[i].Property;
            columnNames[i] = deleteColumns[i].ColumnName;
        }

        return CreateValueSetter(getters, properties, columnNames, "delete");
    }

    /// <summary>
    /// Builds the shared bulk value setter: assigns one entity's values into the command's
    /// existing parameters, matched by position.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26. The three setters previously clamped with <c>Math.Min(getters.Length, pc.Count)</c>
    /// and bound whatever overlapped. That silently absorbed any disagreement between the metadata
    /// that created the parameters (<c>PrepareInsertParameters</c> and friends, from
    /// <c>cached.Metadata</c>) and the metadata these getters were compiled from
    /// (<see cref="ResolveMetadata"/>, resolved independently in this type's static constructor).
    /// The two are resolved separately, so they can disagree - spec 008 §7.1 records
    /// binders-from-a-different-Jaunty-version as a real deployment shape.
    /// </para>
    /// <para>
    /// What the clamp did with a disagreement is the problem: the surplus parameters kept whatever
    /// they last held, the statement executed anyway, and on the bulk path that is the *previous
    /// row's* values written under the current row's key, for every row after the first. A silent
    /// wrong-data write is the worst outcome available here, and it was the default one. Checking
    /// the shape costs a comparison per row and turns it into an error that names the mismatch.
    /// </para>
    /// <para>
    /// The count check runs on every bind (O(1)). The name check runs once <em>per parameter
    /// collection</em>, not once ever: an earlier version memoised it with a plain bool, which was
    /// unsound. The two sides are not equally static - the getters are frozen when
    /// <c>WriteParameterCache&lt;T&gt;</c>'s static constructor runs, but the parameters come from
    /// <c>CrudSqlCache.GetSql&lt;T&gt;(connection)</c>, which is keyed on
    /// <c>(entityType, connectionType)</c> and so re-resolves metadata the first time a given
    /// <typeparamref name="T"/> is used on a new connection type. If the public
    /// <c>JauntyConfig.ReflectionTableMetadataResolver</c> is replaced in between (a supported
    /// operation - <c>JauntyConfig.Reset()</c> and re-registration are both public), the second
    /// resolution can order columns differently while the getters keep the first order. That is an
    /// <em>equal-count reorder</em>: the worst misbind available, and the one the count check cannot
    /// see. Re-checking whenever the collection identity changes costs one reference comparison per
    /// bind - the bulk loops reuse a single collection for every row, so steady-state cost is
    /// unchanged - and closes the case this guard exists for. The field is plain: a race can only
    /// run the check twice, which is harmless.
    /// </para>
    /// </remarks>
    private static Action<IDataParameterCollection, T> CreateValueSetter(
        Func<T, object?>[] getters, PropertyInfo?[] properties, string[] columnNames, string operation)
    {
        IDataParameterCollection? verifiedAgainst = null;

        return (pc, entity) =>
        {
            if (pc.Count != getters.Length)
                throw new InvalidOperationException(
                    $"Bulk {operation} of '{typeof(T).Name}' cannot bind values: the command carries {pc.Count} " +
                    $"parameter(s) but the entity metadata supplies {getters.Length} ({string.Join(", ", columnNames)}). " +
                    "Jaunty binds bulk values by position, so continuing would leave the surplus parameters holding " +
                    "the previous row's values and write wrong data instead of failing. The command's parameters and " +
                    "this setter's getters were resolved from entity metadata at different times - check whether " +
                    "JauntyConfig.ReflectionTableMetadataResolver was replaced, or returns a different shape, between " +
                    "the two resolutions.");

            if (!ReferenceEquals(pc, verifiedAgainst))
            {
                VerifyParameterNames(pc, columnNames, operation);
                verifiedAgainst = pc;
            }

            for (int i = 0; i < getters.Length; i++)
            {
                // IDataParameter, not IDbDataParameter: Value and ParameterName are declared on the
                // former, and the narrower cast made a conforming-but-not-IDbDataParameter entry
                // another way to silently skip a column.
                if (pc[i] is not IDataParameter parameter)
                    throw new InvalidOperationException(
                        $"Bulk {operation} of '{typeof(T).Name}' found a non-parameter entry at index {i} of the " +
                        $"command's parameter collection ('{pc[i]?.GetType().Name ?? "null"}'), where column " +
                        $"'{columnNames[i]}' was expected. Jaunty binds bulk values by position; an entry it cannot " +
                        "set would silently keep the previous row's value.");

                parameter.Value = ParameterBinder.ApplyTypeHandlerIfNeeded(getters[i](entity), properties[i]) ?? DBNull.Value;
            }
        };
    }

    /// <summary>
    /// Confirms each parameter sits at the position its column expects.
    /// </summary>
    /// <remarks>
    /// The prefix is stripped from the <em>parameter's</em> name only, never from the column name.
    /// <c>PrepareInsertParameters</c> and friends write <c>"@" + ColumnName</c> and some providers
    /// normalise or drop that prefix on read-back, so the parameter side needs it removed - but the
    /// column name arrives raw from metadata and stripping it too would mis-handle a column whose
    /// own name begins with a sigil: <c>[Column("$type")]</c> becomes parameter <c>"@$type"</c>,
    /// which strips to <c>"$type"</c>, while the expected side would have stripped to <c>"type"</c>
    /// and thrown on a perfectly correct configuration. A parameter whose name the provider reports
    /// as empty is not checkable and is left alone.
    /// </remarks>
    private static void VerifyParameterNames(IDataParameterCollection pc, string[] columnNames, string operation)
    {
        for (int i = 0; i < columnNames.Length; i++)
        {
            if (pc[i] is not IDataParameter parameter)
                continue; // The bind loop below reports this with the index and expected column.

            string actual = StripParameterPrefix(parameter.ParameterName);
            if (actual.Length == 0)
                continue;

            if (!string.Equals(actual, columnNames[i], StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Bulk {operation} of '{typeof(T).Name}' cannot bind values: parameter {i} is named " +
                    $"'{parameter.ParameterName}' but the entity metadata expects column '{columnNames[i]}' at that " +
                    $"position (full order: {string.Join(", ", columnNames)}). Jaunty binds bulk values by position, " +
                    "so continuing would write each column's value into a different column. The command's parameters " +
                    "and this setter's getters were resolved from entity metadata at different times - check whether " +
                    "JauntyConfig.ReflectionTableMetadataResolver was replaced, or returns a different shape, between " +
                    "the two resolutions.");
        }
    }

    private static string StripParameterPrefix(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        char first = name![0];
        return first is '@' or ':' or '?' or '$' ? name.Substring(1) : name;
    }

    /// <summary>
    /// Returns a strongly-typed property getter delegate for the column. Prefers the
    /// column's reflection-free compiled <see cref="ColumnMetadata.Getter"/> when present
    /// (source-generated metadata); otherwise compiles one from <see cref="ColumnMetadata.Property"/>
    /// using expression trees, ~10x faster than <c>PropertyInfo.GetValue()</c> on repeated calls.
    /// </summary>
    private static Func<T, object?> CreateTypedGetter(ColumnMetadata column)
    {
        if (column.Getter is { } getter)
            return entity => getter(entity!);

        ParameterExpression param = Expression.Parameter(typeof(T), "e");
        MemberExpression access = Expression.Property(param, column.Property!);
        UnaryExpression box = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<T, object?>>(box, param).Compile();
    }

    private static EntityMetadata? ResolveMetadata()
    {
        // 1. Source-generated IEntityMetadataSource implementation - reflection-free
        if (SourceGeneratedMetadataResolver.TryBuild<T>() is EntityMetadata sourceGenMetadata)
            return sourceGenMetadata;

        // 2. Fallback to extension hook (Jaunty.Extensions.Reflection)
        return JauntyConfig.ReflectionTableMetadataResolver?.Invoke(typeof(T)) is EntityMetadata meta ? meta : null;
    }

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2090", Justification = "Reached only when T is not source-generated - a source-generated T implements IGeneratedAccessors<T>, so Bindings.Build takes the delegate from there and never calls this. The remaining population is a hand-written entity supplying its own BindInsert/BindUpdate/BindDelete by convention, which this cannot arrange to preserve: the consumer must root those members (for example with [DynamicDependency]) or implement IGeneratedAccessors<T>. Spec 009.")]
#endif
    private static Action<IDbCommand, T>? TryGetGeneratedBinder(string methodName)
    {

        MethodInfo? method = typeof(T).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, [typeof(IDbCommand), typeof(T)], null);
        return (Action<IDbCommand, T>?)method?.CreateDelegate(typeof(Action<IDbCommand, T>));
    }

    private static Action<IDbCommand, T>? TryGetReflectionBinder(Func<Type, Action<IDbCommand, object>>? resolver)
    {
        return resolver?.Invoke(typeof(T)) is Action<IDbCommand, object> binder ? ((cmd, entity) => binder(cmd, entity!)) : null;
    }

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2075", Justification = "Interfaces implemented by T are preserved because T is a public entity type reachable from the caller's own generic instantiation.")]
    [UnconditionalSuppressMessage("AOT", "IL2090", Justification = "Interfaces implemented by T are preserved because T is a public entity type reachable from the caller's own generic instantiation.")]
#endif
    private static Action<T, long>? CreateIdSetter()
    {
        if (typeof(IEntity).IsAssignableFrom(typeof(T)))
            return static (target, value) => ((IEntity)target!).Id = value;

        // IEntity<TId> covers non-long primary keys (int, Guid, string, ...). Only numeric TId
        // types can receive the database-generated `long` identity value here - a Guid/string key
        // can't come from a database identity column, so those fall through and Id is left
        // whatever the caller already set, matching the pre-existing behavior for entities with no
        // usable setter at all (the call site only invokes IdSetter when non-null).
        Type? entityInterface = Array.Find(typeof(T).GetInterfaces(),
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntity<>));

        if (entityInterface is null)
            return null;

        Type idType = entityInterface.GetGenericArguments()[0];
        if (!IsConvertibleFromInt64(idType))
            return null;

        PropertyInfo idProperty = entityInterface.GetProperty(nameof(IEntity<object>.Id))!;

        ParameterExpression target = Expression.Parameter(typeof(T), "target");
        ParameterExpression value = Expression.Parameter(typeof(long), "value");
        MethodCallExpression setterCall = Expression.Call(
            Expression.Convert(target, entityInterface), idProperty.SetMethod!, Expression.Convert(value, idType));

        return Expression.Lambda<Action<T, long>>(setterCall, target, value).Compile();
    }

    private static bool IsConvertibleFromInt64(Type type) => Type.GetTypeCode(type) switch
    {
        TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or
        TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or
        TypeCode.Single or TypeCode.Double or TypeCode.Decimal => true,
        _ => false
    };
}