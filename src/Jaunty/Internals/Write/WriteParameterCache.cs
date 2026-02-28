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
        var metadata = ResolveMetadata();
        if (metadata == null) return null;

        // Insert uses NonIdentityColumns (excluding computed) — same order as PrepareInsertParameters.
        // Pre-compile property getters to avoid PropertyInfo.GetValue() reflection in the hot loop.
        var columns = metadata.NonIdentityColumns;
        var getters = new Func<T, object?>[columns.Count];
        var isComputed = new bool[columns.Count];
        int getterCount = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            isComputed[i] = columns[i].IsComputed;
            if (!isComputed[i])
            {
                getters[getterCount] = CreateTypedGetter(columns[i].Property);
                getterCount++;
            }
        }

        return (pc, entity) =>
        {
            int count = Math.Min(getterCount, pc.Count);
            for (int i = 0; i < count; i++)
            {
                ((IDbDataParameter)pc[i]).Value = getters[i](entity) ?? DBNull.Value;
            }
        };
    }

    private static Action<IDataParameterCollection, T>? CreateUpdateValueSetter()
    {
        var metadata = ResolveMetadata();
        if (metadata == null) return null;

        // Update: non-key/non-identity/non-computed columns for SET, then primary keys for WHERE.
        // Pre-compile all getters into a single flat array matching parameter order.
        var allColumns = metadata.Columns;
        var primaryKeys = metadata.PrimaryKeys;
        var getterList = new List<Func<T, object?>>();

        // SET clause columns
        for (int i = 0; i < allColumns.Count; i++)
        {
            var col = allColumns[i];
            if (col.IsPrimaryKey || col.IsIdentity || col.IsComputed) continue;
            getterList.Add(CreateTypedGetter(col.Property));
        }
        // WHERE clause primary keys
        for (int i = 0; i < primaryKeys.Count; i++)
        {
            getterList.Add(CreateTypedGetter(primaryKeys[i].Property));
        }

        var getters = getterList.ToArray();
        return (pc, entity) =>
        {
            int count = Math.Min(getters.Length, pc.Count);
            for (int i = 0; i < count; i++)
            {
                ((IDbDataParameter)pc[i]).Value = getters[i](entity) ?? DBNull.Value;
            }
        };
    }

    private static Action<IDataParameterCollection, T>? CreateDeleteValueSetter()
    {
        var metadata = ResolveMetadata();
        if (metadata == null) return null;

        // Delete uses only primary key columns — same order as PrepareDeleteParameters.
        var primaryKeys = metadata.PrimaryKeys;
        var getters = new Func<T, object?>[primaryKeys.Count];
        for (int i = 0; i < primaryKeys.Count; i++)
        {
            getters[i] = CreateTypedGetter(primaryKeys[i].Property);
        }

        return (pc, entity) =>
        {
            int count = Math.Min(getters.Length, pc.Count);
            for (int i = 0; i < count; i++)
            {
                ((IDbDataParameter)pc[i]).Value = getters[i](entity) ?? DBNull.Value;
            }
        };
    }

    /// <summary>
    /// Compiles a strongly-typed property getter delegate using expression trees.
    /// ~10x faster than PropertyInfo.GetValue() on repeated calls.
    /// </summary>
    private static Func<T, object?> CreateTypedGetter(PropertyInfo prop)
    {
        var param = Expression.Parameter(typeof(T), "e");
        var access = Expression.Property(param, prop);
        var box = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<T, object?>>(box, param).Compile();
    }

    private static EntityMetadata? ResolveMetadata()
    {
        if (JauntyConfig.ReflectionTableMetadataResolver?.Invoke(typeof(T)) is EntityMetadata meta)
            return meta;
        return null;
    }

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2090", Justification = "Bind methods are source-generated and always preserved.")]
#endif
    private static Action<IDbCommand, T>? TryGetGeneratedBinder(string methodName)
    {
        var method = typeof(T).GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, [typeof(IDbCommand), typeof(T)], null);
        return (Action<IDbCommand, T>?)method?.CreateDelegate(typeof(Action<IDbCommand, T>));
    }

    private static Action<IDbCommand, T>? TryGetReflectionBinder(Func<Type, Action<IDbCommand, object>>? resolver)
    {
        if (resolver?.Invoke(typeof(T)) is Action<IDbCommand, object> binder)
        {
            return (cmd, entity) => binder(cmd, entity);
        }
        return null;
    }

    private static Action<T, long>? CreateIdSetter()
    {
        if (typeof(IEntity).IsAssignableFrom(typeof(T)))
        {
            return static (target, value) => ((IEntity)target).Id = value;
        }
        return null;
    }
}
