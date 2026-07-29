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
    public static readonly Action<IDbCommand, T>? InsertBinder;
    public static readonly Action<IDbCommand, T>? UpdateBinder;
    public static readonly Action<IDbCommand, T>? DeleteBinder;

    public static readonly Action<IDataParameterCollection, T>? InsertValueSetter;
    public static readonly Action<IDataParameterCollection, T>? UpdateValueSetter;
    public static readonly Action<IDataParameterCollection, T>? DeleteValueSetter;

    public static readonly Action<T, long>? IdSetter;

    static WriteParameterCache()
    {
        InsertBinder = TryGetGeneratedBinder("BindInsert") ?? TryGetReflectionBinder(JauntyConfig.ReflectionInsertBinderResolver);
        UpdateBinder = TryGetGeneratedBinder("BindUpdate") ?? TryGetReflectionBinder(JauntyConfig.ReflectionUpdateBinderResolver);
        DeleteBinder = TryGetGeneratedBinder("BindDelete") ?? TryGetReflectionBinder(JauntyConfig.ReflectionDeleteBinderResolver);

        // Value setters for bulk operations: update values on existing parameters by index.
        // PrepareXxxParameters has already created provider-native parameters on the command;
        // the value setter just walks the collection and sets .Value for each matching param.
        // Metadata is resolved once here (rather than separately inside each Create*ValueSetter)
        // since ResolveMetadata() re-instantiates new T() and rebuilds EntityMetadata from scratch.
        EntityMetadata? metadata = ResolveMetadata();
        InsertValueSetter = InsertBinder != null ? CreateInsertValueSetter(metadata) : null;
        UpdateValueSetter = UpdateBinder != null ? CreateUpdateValueSetter(metadata) : null;
        DeleteValueSetter = DeleteBinder != null ? CreateDeleteValueSetter(metadata) : null;

        IdSetter = CreateIdSetter();
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
    /// The count check runs on every bind (O(1), and the collection can differ between calls); the
    /// name check runs once, since both sides of the comparison are static per <typeparamref name="T"/>
    /// and a per-row string comparison over every column is exactly the cost the bulk path exists
    /// to avoid. The flag is a plain field: a race can only run the check twice, which is harmless.
    /// </para>
    /// </remarks>
    private static Action<IDataParameterCollection, T> CreateValueSetter(
        Func<T, object?>[] getters, PropertyInfo?[] properties, string[] columnNames, string operation)
    {
        bool namesVerified = false;

        return (pc, entity) =>
        {
            if (pc.Count != getters.Length)
                throw new InvalidOperationException(
                    $"Bulk {operation} of '{typeof(T).Name}' cannot bind values: the command carries {pc.Count} " +
                    $"parameter(s) but the entity metadata supplies {getters.Length} ({string.Join(", ", columnNames)}). " +
                    "Jaunty binds bulk values by position, so continuing would leave the surplus parameters holding " +
                    "the previous row's values and write wrong data instead of failing. The parameters were prepared " +
                    "from different entity metadata than the value setter was compiled from - typically a Jaunty core " +
                    "and a source-generated or reflection binder from mismatched versions.");

            if (!namesVerified)
            {
                VerifyParameterNames(pc, columnNames, operation);
                namesVerified = true;
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
    /// Confirms each parameter sits at the position its column expects. Compared without the
    /// dialect's prefix on either side, since <c>PrepareInsertParameters</c> and friends write
    /// <c>"@" + ColumnName</c> and some providers normalise or drop the prefix on read-back;
    /// a parameter whose name the provider reports as empty is not checkable and is left alone.
    /// </summary>
    private static void VerifyParameterNames(IDataParameterCollection pc, string[] columnNames, string operation)
    {
        for (int i = 0; i < columnNames.Length; i++)
        {
            if (pc[i] is not IDataParameter parameter)
                continue; // The bind loop below reports this with the index and expected column.

            string actual = StripParameterPrefix(parameter.ParameterName);
            if (actual.Length == 0)
                continue;

            if (!string.Equals(actual, StripParameterPrefix(columnNames[i]), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Bulk {operation} of '{typeof(T).Name}' cannot bind values: parameter {i} is named " +
                    $"'{parameter.ParameterName}' but the entity metadata expects column '{columnNames[i]}' at that " +
                    $"position (full order: {string.Join(", ", columnNames)}). Jaunty binds bulk values by position, " +
                    "so continuing would write each column's value into a different column. The parameters were " +
                    "prepared from different entity metadata than the value setter was compiled from.");
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
    [UnconditionalSuppressMessage("AOT", "IL2090", Justification = "T is reflected over by method name with no [DynamicDependency], [DynamicallyAccessedMembers] or ILLink descriptor arranging preservation - the suppression hides the report, it does not make the reflection safe. Measured (AUD-R26): samples/NativeAOT-Basic published with PublishAot=true still throws 'No mapper found for type Product'. Annotating T does satisfy the analyzer, but propagates the obligation up through DrDispatcher.Resolve<T>, QueryCore<T> and up to 645 public generic overloads carrying a new()-constrained type parameter (counted from compiled metadata, 2026-07-29), so the real fix is an API-wide annotation pass - specified in docs/specs/009-aot-annotation-pass. Until then, NativeAOT consumers must ensure their entity types are otherwise rooted.")]
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