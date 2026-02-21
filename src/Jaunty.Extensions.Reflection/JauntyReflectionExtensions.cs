using System;
using System.Collections.Generic;
using System.Data;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
using Jaunty.Configuration;
using Jaunty.Internals.Enums;
using Jaunty.Internals.Entity;

namespace Jaunty.Extensions.Reflection;

/// <summary>
/// Provides reflection-based fallback mapping for Jaunty.
/// </summary>
public static class JauntyReflectionExtensions
{
    /// <summary>
    /// Enables reflection-based mapping fallback.
    /// </summary>
#if NET5_0_OR_GREATER
    [RequiresUnreferencedCode("Enables runtime reflection-based mapping which is not trim-safe.")]
#endif
    public static void UseReflectionMapping()
    {
        JauntyConfig.ReflectionMapperResolver = ResolveMapper;
        JauntyConfig.ReflectionInsertBinderResolver = ResolveInsertBinder;
        JauntyConfig.ReflectionUpdateBinderResolver = ResolveUpdateBinder;
        JauntyConfig.ReflectionDeleteBinderResolver = ResolveDeleteBinder;
        JauntyConfig.ReflectionTableMetadataResolver = ResolveTableMetadata;
        JauntyConfig.ReflectionMultiMapperResolver = ResolveMultiMapper;
    }

    private static object ResolveTableMetadata(Type type)
    {
        var method = typeof(MetadataBuilder).GetMethod(nameof(MetadataBuilder.Build), BindingFlags.Public | BindingFlags.Static)!;
        var generic = method.MakeGenericMethod(type);
        return generic.Invoke(null, null)!;
    }

    private static object ResolveMapper(Type type)
    {
        var method = typeof(JauntyReflectionExtensions).GetMethod(nameof(GetTypedMapper), BindingFlags.NonPublic | BindingFlags.Static)!;
        var generic = method.MakeGenericMethod(type);
        return generic.Invoke(null, null)!;
    }

    private static Action<IDbCommand, object> ResolveInsertBinder(Type type)
    {
        var method = typeof(JauntyReflectionExtensions).GetMethod(nameof(GetTypedInsertBinder), BindingFlags.NonPublic | BindingFlags.Static)!;
        var generic = method.MakeGenericMethod(type);
        return (Action<IDbCommand, object>)generic.Invoke(null, null)!;
    }

    private static Action<IDbCommand, object> ResolveUpdateBinder(Type type)
    {
        var method = typeof(JauntyReflectionExtensions).GetMethod(nameof(GetTypedUpdateBinder), BindingFlags.NonPublic | BindingFlags.Static)!;
        var generic = method.MakeGenericMethod(type);
        return (Action<IDbCommand, object>)generic.Invoke(null, null)!;
    }

    private static Action<IDbCommand, object> ResolveDeleteBinder(Type type)
    {
        var method = typeof(JauntyReflectionExtensions).GetMethod(nameof(GetTypedDeleteBinder), BindingFlags.NonPublic | BindingFlags.Static)!;
        var generic = method.MakeGenericMethod(type);
        return (Action<IDbCommand, object>)generic.Invoke(null, null)!;
    }

    private static object ResolveMultiMapper(Type t1, Type t2)
    {
        var method = typeof(JauntyReflectionExtensions).GetMethod(nameof(GetTypedMultiMapper), BindingFlags.NonPublic | BindingFlags.Static)!;
        var generic = method.MakeGenericMethod(t1, t2);
        return generic.Invoke(null, null)!;
    }

    private static Func<IDataReader, T> GetTypedMapper<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
        T>() where T : new()
    {
        return (IDataReader reader) => {
            var setters = MetadataCache<T>.GetSetters(reader, MappingMode.Strict);
            var entity = new T();
            foreach(var setter in setters)
            {
                setter.Set(entity, reader);
            }
            return entity;
        };
    }

    private static Action<IDbCommand, object> GetTypedInsertBinder<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
        T>() where T : new()
    {
        return (cmd, entityObj) => {
            if (entityObj is not T entity) return;
            
            var meta = MetadataCache<T>.Metadata;
            foreach (var col in meta.NonIdentityColumns)
            {
                if (col.IsComputed) continue;
                var param = cmd.CreateParameter();
                param.ParameterName = "@" + col.ColumnName;
                param.Value = col.Property.GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        };
    }

    private static Action<IDbCommand, object> GetTypedUpdateBinder<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
        T>() where T : new()
    {
        return (cmd, entityObj) => {
            if (entityObj is not T entity) return;

            var meta = MetadataCache<T>.Metadata;

            // SET clause parameters: non-key, non-identity, non-computed
            foreach (var col in meta.Columns)
            {
                if (col.IsPrimaryKey || col.IsIdentity || col.IsComputed) continue;
                var param = cmd.CreateParameter();
                param.ParameterName = "@" + col.Property.Name;
                param.Value = col.Property.GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }

            // WHERE clause parameters: primary keys
            foreach (var key in meta.PrimaryKeys)
            {
                var param = cmd.CreateParameter();
                param.ParameterName = "@" + key.Property.Name;
                param.Value = key.Property.GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        };
    }

    private static Action<IDbCommand, object> GetTypedDeleteBinder<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] 
#endif
        T>() where T : new()
    {
        return (cmd, entityObj) => {
            if (entityObj is not T entity) return;

            var meta = MetadataCache<T>.Metadata;

            // WHERE clause parameters: primary keys only
            foreach (var key in meta.PrimaryKeys)
            {
                var param = cmd.CreateParameter();
                param.ParameterName = "@" + key.Property.Name;
                param.Value = key.Property.GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        };
    }

    private static Action<T1, T2, IDataRecord> GetTypedMultiMapper<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
        T1,
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
        T2>() where T1 : new() where T2 : new()
    {
        // Return a delegate that maps a data record to both T1 and T2 by column name.
        // The first time it's called for a reader schema, it builds setter arrays and caches them.
        return (t1, t2, record) =>
        {
            var mapper = MultiEntityMapper<T1, T2>.Get((IDataReader)record);
            mapper.Map(t1, t2, record);
        };
    }
}
