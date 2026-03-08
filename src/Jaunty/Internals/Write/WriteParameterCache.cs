using System.Data;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Linq.Expressions;
using System.Reflection;

using Jaunty.Interfaces;
using Jaunty.Configuration;
using Jaunty.Internals.Entity;

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
        InsertValueSetter = InsertBinder != null ? CreateInsertValueSetter() : null;
        UpdateValueSetter = UpdateBinder != null ? CreateUpdateValueSetter() : null;
        DeleteValueSetter = DeleteBinder != null ? CreateDeleteValueSetter() : null;

        IdSetter = CreateIdSetter();
    }

    private static Action<IDataParameterCollection, T>? CreateInsertValueSetter()
    {
        EntityMetadata? metadata = ResolveMetadata();
        if (metadata == null) return null;

        IReadOnlyList<ColumnMetadata> columns = metadata.InsertColumns;
        var getters = new Func<T, object?>[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            getters[i] = CreateTypedGetter(columns[i].Property);
        }

        return (pc, entity) =>
        {
            int count = Math.Min(getters.Length, pc.Count);

            for (int i = 0; i < count; i++)
                (pc[i] as IDbDataParameter)?.Value = getters[i](entity) ?? DBNull.Value;
        };
    }

    private static Action<IDataParameterCollection, T>? CreateUpdateValueSetter()
    {
        EntityMetadata? metadata = ResolveMetadata();
        if (metadata == null) return null;

        IReadOnlyList<ColumnMetadata> updateColumns = metadata.UpdateColumns;
        IReadOnlyList<ColumnMetadata> primaryKeys = metadata.PrimaryKeys;
        var getters = new Func<T, object?>[updateColumns.Count + primaryKeys.Count];

        for (int i = 0; i < updateColumns.Count; i++)
        {
            getters[i] = CreateTypedGetter(updateColumns[i].Property);
        }
        for (int i = 0; i < primaryKeys.Count; i++)
        {
            getters[updateColumns.Count + i] = CreateTypedGetter(primaryKeys[i].Property);
        }

        return (pc, entity) =>
        {
            int count = Math.Min(getters.Length, pc.Count);
            for (int i = 0; i < count; i++)
            {
                (pc[i] as IDbDataParameter)?.Value = getters[i](entity) ?? DBNull.Value;
            }
        };
    }

    private static Action<IDataParameterCollection, T>? CreateDeleteValueSetter()
    {
        EntityMetadata? metadata = ResolveMetadata();
        if (metadata == null) return null;

        IReadOnlyList<ColumnMetadata> deleteColumns = metadata.DeleteColumns;
        var getters = new Func<T, object?>[deleteColumns.Count];
        for (int i = 0; i < deleteColumns.Count; i++)
        {
            getters[i] = CreateTypedGetter(deleteColumns[i].Property);
        }

        return (pc, entity) =>
        {
            int count = Math.Min(getters.Length, pc.Count);
            for (int i = 0; i < count; i++)
            {
                (pc[i] as IDbDataParameter)?.Value = getters[i](entity) ?? DBNull.Value;
            }
        };
    }

    /// <summary>
    /// Compiles a strongly-typed property getter delegate using expression trees.
    /// ~10x faster than PropertyInfo.GetValue() on repeated calls.
    /// </summary>
    private static Func<T, object?> CreateTypedGetter(PropertyInfo prop)
    {
        ParameterExpression param = Expression.Parameter(typeof(T), "e");
        MemberExpression access = Expression.Property(param, prop);
        UnaryExpression box = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<T, object?>>(box, param).Compile();
    }

    private static EntityMetadata? ResolveMetadata()
    {
        return JauntyConfig.ReflectionTableMetadataResolver?.Invoke(typeof(T)) is EntityMetadata meta ? meta : null;
    }

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2090", Justification = "Bind methods are source-generated and always preserved.")]
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

    private static Action<T, long>? CreateIdSetter()
    {
        if (typeof(IEntity).IsAssignableFrom(typeof(T)))
        {
            return static (target, value) => ((IEntity)target!).Id = value;
        }
        return null;
    }
}