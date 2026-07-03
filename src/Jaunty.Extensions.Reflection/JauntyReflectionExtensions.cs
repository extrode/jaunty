using System;
using System.Data;
using System.Linq;
using System.Reflection;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Extensions.Reflection.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Enums;
using Jaunty.Attributes;
using Jaunty.TypeHandlers;

namespace Jaunty.Extensions.Reflection;

/// <summary>
/// Provides reflection-based fallback mapping for Jaunty.
/// </summary>
/// <remarks>
/// This extension uses runtime reflection and is not compatible with NativeAOT.
/// For NativeAOT scenarios, use the source generator instead.
/// </remarks>
public static class JauntyReflectionExtensions
{
    /// <summary>
    /// Enables reflection-based mapping fallback.
    /// </summary>
    /// <remarks>
    /// This method enables runtime reflection-based mapping which is not trim-safe.
    /// For NativeAOT scenarios, use the source generator instead.
    /// </remarks>
    public static void UseReflectionMapping()
    {
        JauntyConfig.ReflectionMapperResolver = ResolveMapper;
        JauntyConfig.ReflectionInsertBinderResolver = ResolveInsertBinder;
        JauntyConfig.ReflectionUpdateBinderResolver = ResolveUpdateBinder;
        JauntyConfig.ReflectionDeleteBinderResolver = ResolveDeleteBinder;
        JauntyConfig.ReflectionTableMetadataResolver = ResolveTableMetadata;
        JauntyConfig.ReflectionMultiMapperResolver = ResolveMultiMapper;
        JauntyConfig.ReflectionMultiMapperResolverN = ResolveMultiMapperN;
    }

    /// <summary>
    /// Enables native bulk copy support for supported database providers.
    /// This method configures Jaunty to use database-specific bulk copy APIs
    /// (SqlBulkCopy, NpgsqlBinaryImporter, MySqlBulkLoader) for improved performance.
    /// </summary>
    /// <remarks>
    /// Call this method after <see cref="UseReflectionMapping"/> to enable both
    /// reflection-based mapping and native bulk copy support.
    /// <para>
    /// Supported providers:
    /// - SQL Server: Microsoft.Data.SqlClient or System.Data.SqlClient
    /// - PostgreSQL: Npgsql
    /// - MySQL: MySqlConnector or MySql.Data
    /// - SQLite: System.Data.SQLite or Microsoft.Data.Sqlite (optimized INSERT)
    /// </para>
    /// </remarks>
    public static void UseNativeBulkCopy()
    {
        // Register dialect factory that returns dialects with bulk copy support
        // This is a simple approach - in production you might want a more sophisticated factory
        BulkCopyDialectFactory.Enable();
    }

    private static object ResolveTableMetadata(Type type)
    {
        MethodInfo method = typeof(MetadataBuilder).GetMethod(nameof(MetadataBuilder.Build), BindingFlags.Public | BindingFlags.Static)!;
        MethodInfo generic = method.MakeGenericMethod(type);
        return generic.Invoke(null, null)!;
    }

    private static object ResolveMapper(Type type, MappingMode mode)
    {
        MethodInfo method = typeof(JauntyReflectionExtensions).GetMethod(nameof(GetTypedMapper), BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = method.MakeGenericMethod(type);
        var mapperFactory = (Func<MappingMode, Func<IDataReader, object>>)generic.Invoke(null, null)!;
        Func<IDataReader, object> mapper = mapperFactory(mode);
        // Wrap to return correct type
        return CreateTypedMapper(type, mapper);
    }

    private static object CreateTypedMapper(Type type, Func<IDataReader, object> mapper)
    {
        MethodInfo method = typeof(JauntyReflectionExtensions).GetMethod(nameof(WrapMapper), BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = method.MakeGenericMethod(type);
        return generic.Invoke(null, new object[] { mapper })!;
    }

    private static Func<IDataReader, T> WrapMapper<T>(Func<IDataReader, object> mapper) where T : new()
    {
        return reader => (T)mapper(reader);
    }

    private static Action<IDbCommand, object> ResolveInsertBinder(Type type)
    {
        MethodInfo method = typeof(JauntyReflectionExtensions).GetMethod(nameof(GetTypedInsertBinder), BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = method.MakeGenericMethod(type);
        return (Action<IDbCommand, object>)generic.Invoke(null, null)!;
    }

    private static Action<IDbCommand, object> ResolveUpdateBinder(Type type)
    {
        MethodInfo method = typeof(JauntyReflectionExtensions).GetMethod(nameof(GetTypedUpdateBinder), BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = method.MakeGenericMethod(type);
        return (Action<IDbCommand, object>)generic.Invoke(null, null)!;
    }

    private static Action<IDbCommand, object> ResolveDeleteBinder(Type type)
    {
        MethodInfo method = typeof(JauntyReflectionExtensions).GetMethod(nameof(GetTypedDeleteBinder), BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = method.MakeGenericMethod(type);
        return (Action<IDbCommand, object>)generic.Invoke(null, null)!;
    }

    private static object ResolveMultiMapper(Type t1, Type t2)
    {
        MethodInfo method = typeof(JauntyReflectionExtensions).GetMethod(nameof(GetTypedMultiMapper), BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo generic = method.MakeGenericMethod(t1, t2);
        return generic.Invoke(null, null)!;
    }

    private static Action<object, IDataRecord>[] ResolveMultiMapperN(Type[] types, IDataReader reader)
    {
        int arity = types.Length;
        MethodInfo? method = typeof(JauntyReflectionExtensions).GetMethod(
            "BuildMultiMapperNDelegates" + arity,
            BindingFlags.NonPublic | BindingFlags.Static);

        if (method is null)
            throw new InvalidOperationException($"No N-ary multi-mapper helper found for arity {arity}. Supported: 3-7.");

        MethodInfo generic = method.MakeGenericMethod(types);
        return (Action<object, IDataRecord>[])generic.Invoke(null, new object[] { reader })!;
    }

    private static Action<object, IDataRecord>[] BuildMultiMapperNDelegates3<T1, T2, T3>(IDataReader reader)
        where T1 : new() where T2 : new() where T3 : new()
    {
        var m = MultiEntityMapper<T1, T2, T3>.Build(reader);
        return new Action<object, IDataRecord>[]
        {
            (obj, r) => m.ApplyT1((T1)obj, r),
            (obj, r) => m.ApplyT2((T2)obj, r),
            (obj, r) => m.ApplyT3((T3)obj, r),
        };
    }

    private static Action<object, IDataRecord>[] BuildMultiMapperNDelegates4<T1, T2, T3, T4>(IDataReader reader)
        where T1 : new() where T2 : new() where T3 : new() where T4 : new()
    {
        var m = MultiEntityMapper<T1, T2, T3, T4>.Build(reader);
        return new Action<object, IDataRecord>[]
        {
            (obj, r) => m.ApplyT1((T1)obj, r),
            (obj, r) => m.ApplyT2((T2)obj, r),
            (obj, r) => m.ApplyT3((T3)obj, r),
            (obj, r) => m.ApplyT4((T4)obj, r),
        };
    }

    private static Action<object, IDataRecord>[] BuildMultiMapperNDelegates5<T1, T2, T3, T4, T5>(IDataReader reader)
        where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new()
    {
        var m = MultiEntityMapper<T1, T2, T3, T4, T5>.Build(reader);
        return new Action<object, IDataRecord>[]
        {
            (obj, r) => m.ApplyT1((T1)obj, r),
            (obj, r) => m.ApplyT2((T2)obj, r),
            (obj, r) => m.ApplyT3((T3)obj, r),
            (obj, r) => m.ApplyT4((T4)obj, r),
            (obj, r) => m.ApplyT5((T5)obj, r),
        };
    }

    private static Action<object, IDataRecord>[] BuildMultiMapperNDelegates6<T1, T2, T3, T4, T5, T6>(IDataReader reader)
        where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new()
    {
        var m = MultiEntityMapper<T1, T2, T3, T4, T5, T6>.Build(reader);
        return new Action<object, IDataRecord>[]
        {
            (obj, r) => m.ApplyT1((T1)obj, r),
            (obj, r) => m.ApplyT2((T2)obj, r),
            (obj, r) => m.ApplyT3((T3)obj, r),
            (obj, r) => m.ApplyT4((T4)obj, r),
            (obj, r) => m.ApplyT5((T5)obj, r),
            (obj, r) => m.ApplyT6((T6)obj, r),
        };
    }

    private static Action<object, IDataRecord>[] BuildMultiMapperNDelegates7<T1, T2, T3, T4, T5, T6, T7>(IDataReader reader)
        where T1 : new() where T2 : new() where T3 : new() where T4 : new() where T5 : new() where T6 : new() where T7 : new()
    {
        var m = MultiEntityMapper<T1, T2, T3, T4, T5, T6, T7>.Build(reader);
        return new Action<object, IDataRecord>[]
        {
            (obj, r) => m.ApplyT1((T1)obj, r),
            (obj, r) => m.ApplyT2((T2)obj, r),
            (obj, r) => m.ApplyT3((T3)obj, r),
            (obj, r) => m.ApplyT4((T4)obj, r),
            (obj, r) => m.ApplyT5((T5)obj, r),
            (obj, r) => m.ApplyT6((T6)obj, r),
            (obj, r) => m.ApplyT7((T7)obj, r),
        };
    }

    private static Func<MappingMode, Func<IDataReader, object>> GetTypedMapper<T>() where T : new()
    {
        return (MappingMode mode) => (IDataReader reader) =>
        {
            PropertySetter<T>[] setters = MetadataCache<T>.GetSetters(reader, mode);
            var entity = new T();

            foreach (PropertySetter<T> setter in setters)
                setter.Set(entity, reader);

            return entity;
        };
    }


    private static object? ApplyHandlersAndEnumStorage(PropertyInfo property, object? value)
    {
        if (value is null)
            return value;

        Type valueType = value.GetType();

        // Check if there's a registered type handler
        if (TypeHandlerRegistry.HasHandlers && TypeHandlerRegistry.TryGetHandler(valueType, out ITypeHandler? handler) && handler is not null)
        {
            try
            {
                return handler.ToDbValue(value);
            }
            catch
            {
                // Fall through to enum handling
            }
        }

        // Handle enums based on storage strategy
        if (valueType.IsEnum)
        {
            EnumStorageAttribute? enumAttr = property.GetCustomAttribute<EnumStorageAttribute>();
            EnumStorage storage = enumAttr?.Storage ?? JauntyConfig.DefaultEnumStorage;
            
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
                EnumStorageAttribute? enumAttr = property.GetCustomAttribute<EnumStorageAttribute>();
                EnumStorage storage = enumAttr?.Storage ?? JauntyConfig.DefaultEnumStorage;
                
                if (storage == EnumStorage.String)
                {
                    return value.ToString();
                }
            }
        }

        return value;
    }

    private static Action<IDbCommand, object> GetTypedInsertBinder<T>() where T : new()
    {
        return (cmd, entityObj) =>
        {
            if (entityObj is not T entity) return;
            EntityMetadata meta = MetadataCache<T>.Metadata;

            foreach (ColumnMetadata col in meta.InsertColumns)
            {
                IDbDataParameter p = cmd.CreateParameter();
                p.ParameterName = "@" + col.ColumnName;
                object? propValue = col.Property.GetValue(entity);
                p.Value = ApplyHandlersAndEnumStorage(col.Property, propValue) ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
        };
    }

    private static Action<IDbCommand, object> GetTypedUpdateBinder<T>() where T : new()
    {
        return (cmd, entityObj) =>
        {
            if (entityObj is not T entity) return;
            EntityMetadata meta = MetadataCache<T>.Metadata;

            foreach (ColumnMetadata col in meta.UpdateColumns)
            {
                IDbDataParameter p = cmd.CreateParameter();
                p.ParameterName = "@" + col.ColumnName;
                object? propValue = col.Property.GetValue(entity); p.Value = ApplyHandlersAndEnumStorage(col.Property, propValue) ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }

            foreach (ColumnMetadata col in meta.PrimaryKeys)
            {
                IDbDataParameter p = cmd.CreateParameter();
                p.ParameterName = "@" + col.ColumnName;
                object? propValue = col.Property.GetValue(entity); p.Value = ApplyHandlersAndEnumStorage(col.Property, propValue) ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
        };
    }

    private static Action<IDbCommand, object> GetTypedDeleteBinder<T>() where T : new()
    {
        return (cmd, entityObj) =>
        {
            if (entityObj is not T entity) return;
            EntityMetadata meta = MetadataCache<T>.Metadata;

            foreach (ColumnMetadata col in meta.DeleteColumns)
            {
                IDbDataParameter p = cmd.CreateParameter();
                p.ParameterName = "@" + col.ColumnName;
                p.Value = col.Property.GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
        };
    }

    private static Action<T1, T2, IDataRecord> GetTypedMultiMapper<T1, T2>() where T1 : new() where T2 : new()
    {
        return (t1, t2, record) =>
        {
            if (record is not IDataReader reader) return;
            MultiEntityMapper<T1, T2> mapper = MultiEntityMapper<T1, T2>.Get(reader);
            mapper.Map(t1, t2, record);
        };
    }
}