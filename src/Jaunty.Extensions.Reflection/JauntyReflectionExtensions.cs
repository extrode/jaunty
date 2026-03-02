using System;
using System.Data;
using System.Linq;
using System.Reflection;

using Jaunty.Configuration;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Enums;

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

    private static Func<MappingMode, Func<IDataReader, object>> GetTypedMapper<T>() where T : new()
    {
        return (MappingMode mode) => (IDataReader reader) =>
        {
            PropertySetter<T>[] setters = MetadataCache<T>.GetSetters(reader, mode);
            var entity = new T();

            foreach (var setter in setters)
                setter.Set(entity, reader);

            return entity;
        };
    }

    private static Action<IDbCommand, object> GetTypedInsertBinder<T>() where T : new()
    {
        return (cmd, entityObj) =>
        {
            if (entityObj is not T entity) return;
            EntityMetadata meta = MetadataCache<T>.Metadata;

            foreach (var col in meta.InsertColumns)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = "@" + col.ColumnName;
                p.Value = col.Property.GetValue(entity) ?? DBNull.Value;
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

            foreach (var col in meta.UpdateColumns)
            {
                IDbDataParameter p = cmd.CreateParameter();
                p.ParameterName = "@" + col.ColumnName;
                p.Value = col.Property.GetValue(entity) ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }

            foreach (var col in meta.PrimaryKeys)
            {
                IDbDataParameter p = cmd.CreateParameter();
                p.ParameterName = "@" + col.ColumnName;
                p.Value = col.Property.GetValue(entity) ?? DBNull.Value;
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

            foreach (var col in meta.DeleteColumns)
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
