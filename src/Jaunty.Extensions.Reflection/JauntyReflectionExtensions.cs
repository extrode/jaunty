using System;
using System.Collections.Generic;
using System.Data;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Linq;
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

    private static object ResolveMapper(Type type, MappingMode mode)
    {
        var method = typeof(JauntyReflectionExtensions).GetMethod(nameof(GetTypedMapper), BindingFlags.NonPublic | BindingFlags.Static)!;
        var generic = method.MakeGenericMethod(type);
        var mapperFactory = (Func<MappingMode, Func<IDataReader, object>>)generic.Invoke(null, null)!;
        var mapper = mapperFactory(mode);
        // Wrap to return correct type
        return CreateTypedMapper(type, mapper);
    }
    
    private static object CreateTypedMapper(Type type, Func<IDataReader, object> mapper)
    {
        var method = typeof(JauntyReflectionExtensions).GetMethod(nameof(WrapMapper), BindingFlags.NonPublic | BindingFlags.Static)!;
        var generic = method.MakeGenericMethod(type);
        return generic.Invoke(null, new object[] { mapper })!;
    }
    
    private static Func<IDataReader, T> WrapMapper<T>(Func<IDataReader, object> mapper) where T : new()
    {
        return reader => (T)mapper(reader);
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

    private static Func<MappingMode, Func<IDataReader, object>> GetTypedMapper<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
#endif
        T>() where T : new()
    {
        return (MappingMode mode) => (IDataReader reader) => {
            var setters = MetadataCache<T>.GetSetters(reader, mode);
            var entity = new T();
            foreach(var setter in setters)
            {
                setter.Set(entity, reader);
            }
            return (object)entity;
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
                var p = cmd.CreateParameter();
                p.ParameterName = "@" + col.Property.Name;
                p.Value = col.Property.GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(p);
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
            // SET
            foreach (var col in meta.Columns.Where(c => !c.IsPrimaryKey && !c.IsIdentity))
            {
                var p = cmd.CreateParameter();
                p.ParameterName = "@" + col.Property.Name;
                p.Value = col.Property.GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            // WHERE
            foreach (var col in meta.PrimaryKeys)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = "@" + col.Property.Name;
                p.Value = col.Property.GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(p);
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
            foreach (var col in meta.PrimaryKeys)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = "@" + col.Property.Name;
                p.Value = col.Property.GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
        };
    }

    private static Action<T1, T2, IDataRecord> GetTypedMultiMapper<
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T1,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T2
#else
        T1, T2
#endif
    >() where T1 : new() where T2 : new()
    {
        return (t1, t2, record) => {
            if (record is not IDataReader reader) return;
            var mapper = MultiEntityMapper<T1, T2>.Get(reader);
            mapper.Map(t1, t2, record);
        };
    }
}
