using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

using Jaunty.Configuration;
using Jaunty.Dialects;
using Jaunty.Extensions.Reflection.Dialects;
using Jaunty.Internals;
using Jaunty.Internals.Entity;
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

        // Dictionary/KeyValuePair/ValueTuple/dynamic have no mappable properties, so
        // DrDispatcher must resolve them via SpecialTypeMapperResolver before ever
        // falling back to ReflectionMapperResolver's MetadataCache<T>-based mapper.
        // Registering it here (idempotent via SpecialTypeMappers.Register's ??=)
        // means callers don't need to know to call it separately.
        SpecialTypeMappers.Register();
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

    // AUD-R25: every one of these resolvers used to redo its reflection plumbing on each call -
    // typeof(...).GetMethod(name, BindingFlags...), then MakeGenericMethod, then Invoke - with
    // nothing cached between calls. ResolveMapper was the worst: DrDispatcher.Resolve calls it for
    // every query that falls back to reflection mapping, i.e. every query over a non-source-generated
    // entity, which is the whole point of this package, and each call did two GetMethod lookups, two
    // MakeGenericMethod constructions and three Invokes before MetadataCache<T>'s own cache was even
    // consulted. ResolveMultiMapperN additionally built its method name by string concatenation and
    // looked it up by reflection per multi-entity query.
    //
    // This was the one layer of the package that wasn't cached - MetadataCache<T> caches metadata,
    // setters and getters; MultiEntityMapper<...>.Get caches per schema; PostgreSqlBulkCopyProvider
    // added WriteMethodCache/WriteAsyncMethodCache with a comment noting that MakeGenericMethod
    // "is expensive ... and works against the entire point" of the fast path. The same reasoning
    // applies here, one level up.
    //
    // The MethodInfo lookups are hoisted to static readonly fields; the constructed delegates are
    // keyed on the entity type, since each is stateless with respect to the reader and the command
    // (the mapper resolves setters per reader inside its own closure, and the binders capture only
    // per-type converters).
    private static readonly MethodInfo MetadataBuildMethod =
        typeof(MetadataBuilder).GetMethod(nameof(MetadataBuilder.Build), BindingFlags.Public | BindingFlags.Static)!;

    private static readonly MethodInfo GetTypedMapperMethod = NonPublicStatic(nameof(GetTypedMapper));
    private static readonly MethodInfo WrapMapperMethod = NonPublicStatic(nameof(WrapMapper));
    private static readonly MethodInfo GetTypedInsertBinderMethod = NonPublicStatic(nameof(GetTypedInsertBinder));
    private static readonly MethodInfo GetTypedUpdateBinderMethod = NonPublicStatic(nameof(GetTypedUpdateBinder));
    private static readonly MethodInfo GetTypedDeleteBinderMethod = NonPublicStatic(nameof(GetTypedDeleteBinder));
    private static readonly MethodInfo GetTypedMultiMapperMethod = NonPublicStatic(nameof(GetTypedMultiMapper));

    // AUD-R26 (batch 4). Every one of these is derived from entity metadata, and metadata is built
    // from JauntyConfig's schema/table/column name resolvers - public, settable at any time. Keyed
    // on the entity type alone, the first resolution of a given T fixed its columns for the life of
    // the process, so a resolver registered afterwards was invisible to the whole package: the
    // caches below, MetadataCache<T> underneath them, and CrudSqlCache above them all kept the
    // pre-change answer. ConfigurationScoped tags each entry with the generation it was built
    // under; see Jaunty.Internals.ConfigurationGeneration.
    private static readonly ConcurrentDictionary<Type, ConfigurationScoped<object>> TableMetadataCache = new();
    private static readonly ConcurrentDictionary<(Type Type, MappingMode Mode), ConfigurationScoped<object>> MapperCache = new();
    private static readonly ConcurrentDictionary<Type, ConfigurationScoped<Action<IDbCommand, object>>> InsertBinderCache = new();
    private static readonly ConcurrentDictionary<Type, ConfigurationScoped<Action<IDbCommand, object>>> UpdateBinderCache = new();
    private static readonly ConcurrentDictionary<Type, ConfigurationScoped<Action<IDbCommand, object>>> DeleteBinderCache = new();
    private static readonly ConcurrentDictionary<(Type, Type), ConfigurationScoped<object>> MultiMapperCache = new();

    /// <summary>
    /// <see cref="ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey, Func{TKey, TValue})"/> with the
    /// configuration generation as part of what counts as a hit.
    /// </summary>
    /// <remarks>
    /// The build delegates passed in are all non-capturing (<c>static</c>) lambdas, which the
    /// compiler caches, so routing them through here costs no allocation over the GetOrAdd calls it
    /// replaces. Two threads racing on the same key both build and the last write wins; both results
    /// were built from the configuration they recorded, so either is correct.
    /// </remarks>
    private static TValue GetOrBuild<TKey, TValue>(
        ConcurrentDictionary<TKey, ConfigurationScoped<TValue>> cache, TKey key, Func<TKey, TValue> build)
        where TKey : notnull
    {
        // Read the generation before the lookup, never after: see ConfigurationGeneration.Current.
        int generation = ConfigurationGeneration.Current;

        if (cache.TryGetValue(key, out ConfigurationScoped<TValue> cached) && cached.Generation == generation)
            return cached.Value;

        TValue value = build(key);
        cache[key] = new ConfigurationScoped<TValue>(generation, value);
        return value;
    }

    // Keyed on arity, not on the type arguments: the constructed generic method still has to be
    // built per type-set, but the name lookup - a string concatenation plus a reflection search -
    // does not.
    private static readonly ConcurrentDictionary<int, MethodInfo> MultiMapperNMethodCache = new();

    private static MethodInfo NonPublicStatic(string name) =>
        typeof(JauntyReflectionExtensions).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static object ResolveTableMetadata(Type type) =>
        GetOrBuild(TableMetadataCache, type, static t => MetadataBuildMethod.MakeGenericMethod(t).Invoke(null, null)!);

    private static object ResolveMapper(Type type, MappingMode mode) =>
        GetOrBuild(MapperCache, (Type: type, Mode: mode), static key =>
        {
            var mapperFactory = (Func<MappingMode, Func<IDataReader, object>>)
                GetTypedMapperMethod.MakeGenericMethod(key.Type).Invoke(null, null)!;

            Func<IDataReader, object> mapper = mapperFactory(key.Mode);

            // Wrap to return correct type
            return CreateTypedMapper(key.Type, mapper);
        });

    private static object CreateTypedMapper(Type type, Func<IDataReader, object> mapper) =>
        WrapMapperMethod.MakeGenericMethod(type).Invoke(null, new object[] { mapper })!;

    private static Func<IDataReader, T> WrapMapper<T>(Func<IDataReader, object> mapper) where T : new()
    {
        return reader => (T)mapper(reader);
    }

    private static Action<IDbCommand, object> ResolveInsertBinder(Type type) =>
        GetOrBuild(InsertBinderCache, type, static t =>
            (Action<IDbCommand, object>)GetTypedInsertBinderMethod.MakeGenericMethod(t).Invoke(null, null)!);

    private static Action<IDbCommand, object> ResolveUpdateBinder(Type type) =>
        GetOrBuild(UpdateBinderCache, type, static t =>
            (Action<IDbCommand, object>)GetTypedUpdateBinderMethod.MakeGenericMethod(t).Invoke(null, null)!);

    private static Action<IDbCommand, object> ResolveDeleteBinder(Type type) =>
        GetOrBuild(DeleteBinderCache, type, static t =>
            (Action<IDbCommand, object>)GetTypedDeleteBinderMethod.MakeGenericMethod(t).Invoke(null, null)!);

    private static object ResolveMultiMapper(Type t1, Type t2) =>
        GetOrBuild(MultiMapperCache, (t1, t2), static key =>
            GetTypedMultiMapperMethod.MakeGenericMethod(key.Item1, key.Item2).Invoke(null, null)!);

    // Guarded: the attribute's netstandard2.0 SDK polyfill is internal to its own file, so it is
    // not referenceable there - and the trim analyzer only runs for the net8.0 target anyway.
#if NET8_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2060",
        Justification = "The helper MethodInfo now arrives via a cache, so the trimmer can no longer "
                      + "follow it back to the GetMethod call it came from. The BuildMultiMapperNDelegates* "
                      + "helpers are private methods of this type and are always preserved with it; this "
                      + "package is documented as not trim-safe and NativeAOT users take the source generator.")]
#endif
    private static Action<object, IDataRecord>[] ResolveMultiMapperN(Type[] types, IDataReader reader)
    {
        // The result is not cached: BuildMultiMapperNDelegates* binds against this reader's column
        // layout, so it is per-call by construction. Only the helper lookup is hoisted.
        MethodInfo method = MultiMapperNMethodCache.GetOrAdd(types.Length, static arity =>
            typeof(JauntyReflectionExtensions).GetMethod(
                "BuildMultiMapperNDelegates" + arity,
                BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"No N-ary multi-mapper helper found for arity {arity}. Supported: 3-7."));

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


    /// <summary>
    /// Builds a per-property value converter once (at binder-build time, mirroring
    /// <see cref="MetadataCache{T}.CreateFallbackSetter"/> on the read path), so
    /// <see cref="EnumStorageAttribute"/> is resolved once per column instead of once per row.
    /// An explicit attribute is immutable and safe to bake in; a property without one falls back
    /// to <see cref="JauntyConfig.DefaultEnumStorage"/>, which is mutable process-wide state
    /// (callers can change it at runtime, e.g. tests or multi-tenant apps) and so must be
    /// re-checked on every call rather than captured once - same reasoning CreateSetter already
    /// applies to TypeHandlerRegistry, which is likewise re-checked on every call below.
    /// </summary>
    private static Func<object?, object?> BuildValueConverter(PropertyInfo property)
    {
        Type propertyType = property.PropertyType;
        Type underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (underlyingType.IsEnum)
        {
            EnumStorageAttribute? enumAttr = property.GetCustomAttribute<EnumStorageAttribute>();

            if (enumAttr is not null)
            {
                return enumAttr.Storage == EnumStorage.String
                    ? value => ConvertWithTypeHandlerOrElse(value, static v => v.ToString())
                    : value => ConvertWithTypeHandlerOrElse(value, static v => v);
            }

            return value => ConvertWithTypeHandlerOrElse(value, static v =>
                JauntyConfig.DefaultEnumStorage == EnumStorage.String ? v.ToString() : v);
        }

        return value => ConvertWithTypeHandlerOrElse(value, static v => v);
    }

    private static object? ConvertWithTypeHandlerOrElse(object? value, Func<object, object?> fallback)
    {
        if (value is null)
            return value;

        // AUD-R35-115: shared contract with ParameterBinder and GeneratedBindingSupport.
        if (TypeHandlerRegistry.HasHandlers && TypeHandlerRegistry.TryGetHandler(value.GetType(), out ITypeHandler? handler) && handler is not null)
            return TypeHandlerRegistry.ToDbValueOrThrow(handler, value);

        return fallback(value);
    }

    /// <summary>
    /// AUD-R35-068. The three write binders read each value with
    /// <see cref="PropertyInfo.GetValue(object)"/>, once per column per entity, on the hot
    /// insert/update/delete path this package exists to serve. This package already compiles an
    /// expression-tree getter for every column of every entity at snapshot-build time
    /// (<c>MetadataCache.CreateGetter</c>, exposed as <c>PropertyContext&lt;T&gt;.Getter</c>) - and
    /// nothing in <c>src/</c> or <c>tests/</c> ever read it, so the compile cost was paid and the
    /// benefit never collected. Core Jaunty already treats reflection <c>GetValue</c> as the
    /// fallback and the compiled getter as the fast path in the equivalent write code
    /// (<c>WriteParameterCache</c>, <c>Upsert</c>, <c>InsertBuilder</c>); this package was the one
    /// write path that never took it. A column whose property is somehow absent from the snapshot
    /// keeps the reflection getter rather than failing.
    /// </summary>
    private static (string ParamName, Func<T, object?> Get, Func<object?, object?> Convert)[] BuildColumnConverters<T>(IReadOnlyList<ColumnMetadata> columns)
        where T : new()
    {
        PropertyContext<T>[] contexts = MetadataCache<T>.Properties;

        var result = new (string, Func<T, object?>, Func<object?, object?>)[columns.Count];
        for (int i = 0; i < columns.Count; i++)
        {
            PropertyInfo property = columns[i].Property!;
            result[i] = ("@" + columns[i].ColumnName, ResolveGetter(contexts, property), BuildValueConverter(property));
        }
        return result;
    }

    private static Func<T, object?> ResolveGetter<T>(PropertyContext<T>[] contexts, PropertyInfo property)
    {
        for (int i = 0; i < contexts.Length; i++)
        {
            if (contexts[i].Property == property)
                return contexts[i].Getter;
        }

        return entity => property.GetValue(entity);
    }

    private static Action<IDbCommand, object> GetTypedInsertBinder<T>() where T : new()
    {
        var converters = BuildColumnConverters<T>(MetadataCache<T>.Metadata.InsertColumns);

        return (cmd, entityObj) =>
        {
            if (entityObj is not T entity)
                throw new InvalidOperationException($"Expected an instance of '{typeof(T).Name}' but received '{entityObj?.GetType().Name ?? "null"}'.");

            foreach ((string paramName, Func<T, object?> get, Func<object?, object?> convert) in converters)
            {
                IDbDataParameter p = cmd.CreateParameter();
                p.ParameterName = paramName;
                object? propValue = get(entity);
                p.Value = convert(propValue) ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
        };
    }

    private static Action<IDbCommand, object> GetTypedUpdateBinder<T>() where T : new()
    {
        EntityMetadata meta = MetadataCache<T>.Metadata;
        var updateConverters = BuildColumnConverters<T>(meta.UpdateColumns);
        var keyConverters = BuildColumnConverters<T>(meta.PrimaryKeys);

        return (cmd, entityObj) =>
        {
            if (entityObj is not T entity)
                throw new InvalidOperationException($"Expected an instance of '{typeof(T).Name}' but received '{entityObj?.GetType().Name ?? "null"}'.");

            foreach ((string paramName, Func<T, object?> get, Func<object?, object?> convert) in updateConverters)
            {
                IDbDataParameter p = cmd.CreateParameter();
                p.ParameterName = paramName;
                object? propValue = get(entity);
                p.Value = convert(propValue) ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }

            foreach ((string paramName, Func<T, object?> get, Func<object?, object?> convert) in keyConverters)
            {
                IDbDataParameter p = cmd.CreateParameter();
                p.ParameterName = paramName;
                object? propValue = get(entity);
                p.Value = convert(propValue) ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
        };
    }

    private static Action<IDbCommand, object> GetTypedDeleteBinder<T>() where T : new()
    {
        var converters = BuildColumnConverters<T>(MetadataCache<T>.Metadata.DeleteColumns);

        return (cmd, entityObj) =>
        {
            if (entityObj is not T entity)
                throw new InvalidOperationException($"Expected an instance of '{typeof(T).Name}' but received '{entityObj?.GetType().Name ?? "null"}'.");

            foreach ((string paramName, Func<T, object?> get, Func<object?, object?> convert) in converters)
            {
                IDbDataParameter p = cmd.CreateParameter();
                p.ParameterName = paramName;
                object? propValue = get(entity);
                p.Value = convert(propValue) ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
        };
    }

    private static Action<T1, T2, IDataRecord> GetTypedMultiMapper<T1, T2>() where T1 : new() where T2 : new()
    {
        return (t1, t2, record) =>
        {
            if (record is not IDataReader reader)
                throw new InvalidOperationException($"Multi-entity mapping requires an '{nameof(IDataReader)}', but received '{record?.GetType().Name ?? "null"}'.");
            MultiEntityMapper<T1, T2> mapper = MultiEntityMapper<T1, T2>.Get(reader);
            mapper.Map(t1, t2, record);
        };
    }
}