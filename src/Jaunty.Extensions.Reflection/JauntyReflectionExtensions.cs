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
        JauntyConfig.ReflectionTableMetadataResolver = ResolveTableMetadata;
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
                var param = cmd.CreateParameter();
                param.ParameterName = "@" + col.ColumnName;
                param.Value = col.Property.GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(param);
            }
        };
    }
}
