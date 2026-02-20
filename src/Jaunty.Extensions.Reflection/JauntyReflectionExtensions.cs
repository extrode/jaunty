using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Jaunty.Configuration;

namespace Jaunty.Extensions.Reflection;

/// <summary>
/// Provides reflection-based fallback mapping for Jaunty.
/// </summary>
public static class JauntyReflectionExtensions
{
    /// <summary>
    /// Enables reflection-based mapping fallback.
    /// This should be called during application startup if you are not using source generation for all entities.
    /// </summary>
    [RequiresUnreferencedCode("Enables runtime reflection-based mapping which is not trim-safe.")]
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
        // Internal decision: Should we use DataReader or DbDataReader? 
        // For fallback simplicity, we'll use IDataReader as it's the most compatible.
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

    private static Func<IDataReader, T> GetTypedMapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>() where T : new()
    {
        // This will call the MetadataCache<T> that we moved to this assembly
        return (IDataReader reader) => {
            var setters = MetadataCache<T>.GetSetters(reader, Jaunty.Internals.Enums.MappingMode.Strict);
            var entity = new T();
            foreach(var setter in setters)
            {
                setter.Set(entity, reader);
            }
            return entity;
        };
    }

    private static Action<IDbCommand, object> GetTypedInsertBinder<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>() where T : class, new()
    {
        return (cmd, entity) => {
            // Logic to bind parameters using reflection
            // (We will move the WriteParameterCache logic here or similar)
        };
    }
}
