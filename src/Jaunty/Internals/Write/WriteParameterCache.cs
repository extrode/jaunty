using System.Data;
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

        // Insert uses NonIdentityColumns (excluding computed) — same order as PrepareInsertParameters
        var columns = metadata.NonIdentityColumns;
        return (pc, entity) =>
        {
            int paramIndex = 0;
            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (col.IsComputed) continue;
                if (paramIndex < pc.Count)
                    ((IDbDataParameter)pc[paramIndex]).Value = col.Property.GetValue(entity) ?? DBNull.Value;
                paramIndex++;
            }
        };
    }

    private static Action<IDataParameterCollection, T>? CreateUpdateValueSetter()
    {
        var metadata = ResolveMetadata();
        if (metadata == null) return null;

        // Update: non-key/non-identity/non-computed columns for SET, then primary keys for WHERE
        // Same order as PrepareUpdateParameters
        var allColumns = metadata.Columns;
        var primaryKeys = metadata.PrimaryKeys;
        return (pc, entity) =>
        {
            int paramIndex = 0;
            // SET clause parameters
            for (int i = 0; i < allColumns.Count; i++)
            {
                var col = allColumns[i];
                if (col.IsPrimaryKey || col.IsIdentity || col.IsComputed) continue;
                if (paramIndex < pc.Count)
                    ((IDbDataParameter)pc[paramIndex]).Value = col.Property.GetValue(entity) ?? DBNull.Value;
                paramIndex++;
            }
            // WHERE clause parameters (primary keys)
            for (int i = 0; i < primaryKeys.Count; i++)
            {
                var key = primaryKeys[i];
                if (paramIndex < pc.Count)
                    ((IDbDataParameter)pc[paramIndex]).Value = key.Property.GetValue(entity) ?? DBNull.Value;
                paramIndex++;
            }
        };
    }

    private static Action<IDataParameterCollection, T>? CreateDeleteValueSetter()
    {
        var metadata = ResolveMetadata();
        if (metadata == null) return null;

        // Delete uses only primary key columns — same order as PrepareDeleteParameters
        var primaryKeys = metadata.PrimaryKeys;
        return (pc, entity) =>
        {
            for (int i = 0; i < primaryKeys.Count; i++)
            {
                var key = primaryKeys[i];
                if (i < pc.Count)
                    ((IDbDataParameter)pc[i]).Value = key.Property.GetValue(entity) ?? DBNull.Value;
            }
        };
    }

    private static EntityMetadata? ResolveMetadata()
    {
        if (JauntyConfig.ReflectionTableMetadataResolver?.Invoke(typeof(T)) is EntityMetadata meta)
            return meta;
        return null;
    }

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
