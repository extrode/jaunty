
using System.Data;

using Jaunty.Attributes;
using Jaunty.Interceptors;
using Jaunty.Internals.Enums;
using Jaunty.TypeHandlers;

namespace Jaunty.Configuration;

/// <summary>
/// Provides global configuration options for Jaunty's entity-to-database mapping behavior.
/// </summary>
/// <remarks>
/// <para><b>Thread safety:</b> every setting is an atomic, immediately visible write
/// (volatile fields), and the interceptor mutation methods
/// (<see cref="AddInterceptor"/>, <see cref="AddInterceptors"/>, <see cref="ClearInterceptors"/>)
/// are synchronized, so concurrent registration cannot lose interceptors. Commands that
/// are already executing keep the configuration they observed at their start.</para>
/// <para><see cref="Reset"/> is <b>not</b> atomic as a whole (each individual field reset
/// is); it is intended for test cleanup, not for reconfiguring a live application.
/// For deterministic behavior, configure Jaunty once at application startup.</para>
/// </remarks>
public static class JauntyConfig
{
    private static readonly object InterceptorSync = new();

    private static volatile Func<Type, string>? _schemaNameResolver;
    private static volatile Func<Type, string>? _tableNameResolver;
    private static volatile Func<string, string>? _columnNameResolver;
    private static volatile Action<string, object?>? _logger;
    private static volatile Func<Type, MappingMode, object>? _reflectionMapperResolver;
    private static volatile Func<Type, IDataReader, object>? _specialTypeMapperResolver;
    private static volatile Func<Type, Action<IDbCommand, object>>? _reflectionInsertBinderResolver;
    private static volatile Func<Type, Action<IDbCommand, object>>? _reflectionUpdateBinderResolver;
    private static volatile Func<Type, Action<IDbCommand, object>>? _reflectionDeleteBinderResolver;
    private static volatile Func<Type, object>? _reflectionTableMetadataResolver;
    private static volatile Func<Type, Type, object>? _reflectionMultiMapperResolver;
    private static volatile Func<Type[], IDataReader, Action<object, IDataRecord>[]>? _reflectionMultiMapperResolverN;
    private static volatile InterceptorPipeline? _interceptorPipeline;
    private static volatile EnumStorage _defaultEnumStorage = EnumStorage.Numeric;

    private static volatile int _parameterParsingCapacity = 8;
    private static volatile int _queryResultCapacity = 64;
    private static volatile int _csvFieldCapacity = 16;

    /// <summary>
    /// Gets or sets the initial capacity for parameter name lists when parsing SQL.
    /// Default: 8. Most queries have fewer than 8 parameters.
    /// </summary>
    public static int ParameterParsingCapacity
    {
        get => _parameterParsingCapacity;
        set => _parameterParsingCapacity = value > 0 ? value : 8;
    }

    /// <summary>
    /// Gets or sets the initial capacity for query result lists.
    /// Default: 64. Suitable for most queries without immediate reallocation.
    /// </summary>
    public static int QueryResultCapacity
    {
        get => _queryResultCapacity;
        set => _queryResultCapacity = value > 0 ? value : 64;
    }

    /// <summary>
    /// Gets or sets the initial capacity for CSV field lists when parsing CSV rows.
    /// Default: 16. Typical CSV files have fewer than 16 columns.
    /// </summary>
    public static int CsvFieldCapacity
    {
        get => _csvFieldCapacity;
        set => _csvFieldCapacity = value > 0 ? value : 16;
    }

    /// <summary>
    /// Gets or sets a custom resolver for schema names.
    /// </summary>
    public static Func<Type, string>? SchemaNameResolver
    {
        get => _schemaNameResolver;
        set => _schemaNameResolver = value;
    }

    /// <summary>
    /// Gets or sets a custom resolver for table names.
    /// </summary>
    public static Func<Type, string>? TableNameResolver
    {
        get => _tableNameResolver;
        set => _tableNameResolver = value;
    }

    /// <summary>
    /// Gets or sets a custom resolver for column names.
    /// </summary>
    public static Func<string, string>? ColumnNameResolver
    {
        get => _columnNameResolver;
        set => _columnNameResolver = value;
    }

    /// <summary>
    /// Gets or sets a global diagnostic logger for SQL commands.
    /// </summary>
    public static Action<string, object?>? Logger
    {
        get => _logger;
        set => _logger = value;
    }

    /// <summary>
    /// Optional fallback mapper resolver for types that are not source-generated.
    /// Typically provided by Jaunty.Extensions.Reflection.
    /// Returns a Func&lt;IDataReader, T&gt; or Func&lt;DbDataReader, T&gt; cast to object.
    /// </summary>
    public static Func<Type, MappingMode, object>? ReflectionMapperResolver
    {
        get => _reflectionMapperResolver;
        set => _reflectionMapperResolver = value;
    }

    /// <summary>
    /// Optional fallback parameter binder resolver for INSERT operations.
    /// </summary>
    public static Func<Type, Action<IDbCommand, object>>? ReflectionInsertBinderResolver
    {
        get => _reflectionInsertBinderResolver;
        set => _reflectionInsertBinderResolver = value;
    }

    /// <summary>
    /// Optional fallback parameter binder resolver for UPDATE operations.
    /// </summary>
    public static Func<Type, Action<IDbCommand, object>>? ReflectionUpdateBinderResolver
    {
        get => _reflectionUpdateBinderResolver;
        set => _reflectionUpdateBinderResolver = value;
    }

    /// <summary>
    /// Optional fallback parameter binder resolver for DELETE operations.
    /// </summary>
    public static Func<Type, Action<IDbCommand, object>>? ReflectionDeleteBinderResolver
    {
        get => _reflectionDeleteBinderResolver;
        set => _reflectionDeleteBinderResolver = value;
    }

    /// <summary>
    /// Optional fallback table metadata resolver for types that are not source-generated.
    /// Returns an EntityMetadata object.
    /// </summary>
    public static Func<Type, object>? ReflectionTableMetadataResolver
    {
        get => _reflectionTableMetadataResolver;
        set => _reflectionTableMetadataResolver = value;
    }

    /// <summary>
    /// Optional fallback multi-mapper resolver for types that are not source-generated.
    /// Returns a MultiEntityMapper object.
    /// </summary>
    public static Func<Type, Type, object>? ReflectionMultiMapperResolver
    {
        get => _reflectionMultiMapperResolver;
        set => _reflectionMultiMapperResolver = value;
    }

    /// <summary>
    /// Optional fallback multi-mapper resolver for arity-3+ multi-entity queries.
    /// Key: array of N Type objects (one per entity type position).
    /// Value: array of N Action&lt;object, IDataRecord&gt; delegates (one per type position).
    /// Registered by Jaunty.Extensions.Reflection via UseReflectionMapping().
    /// </summary>
    public static Func<Type[], IDataReader, Action<object, IDataRecord>[]>? ReflectionMultiMapperResolverN
    {
        get => _reflectionMultiMapperResolverN;
        set => _reflectionMultiMapperResolverN = value;
    }


    /// <summary>
    /// Optional resolver for special types (Dictionary, KeyValuePair, ValueTuple, ExpandoObject).
    /// Typically provided by Jaunty.Extensions.Reflection.SpecialTypeMappers.Register().
    /// Returns a Func&lt;IDataReader, object&gt; that creates the special type instance.
    /// </summary>
    public static Func<Type, IDataReader, object>? SpecialTypeMapperResolver
    {
        get => _specialTypeMapperResolver;
        set => _specialTypeMapperResolver = value;
    }

    /// <summary>
    /// Gets or sets the global default enum storage strategy.
    /// Default: EnumStorage.Numeric.
    /// </summary>
    public static EnumStorage DefaultEnumStorage
    {
        get => _defaultEnumStorage;
        set => _defaultEnumStorage = value;
    }

    /// <summary>
    /// Registers a type handler using delegate-based conversion functions.
    /// </summary>
    public static void RegisterTypeHandler<T>(Func<object?, T> fromDb, Func<T?, object?> toDb)
    {
        if (fromDb is null) throw new ArgumentNullException(nameof(fromDb));
        if (toDb is null) throw new ArgumentNullException(nameof(toDb));
        TypeHandlerRegistry.Register<T>(new DelegateTypeHandler<T>(fromDb, toDb));
    }

    /// <summary>
    /// Registers a type handler using a TypeHandler instance.
    /// </summary>
    public static void RegisterTypeHandler<T>(TypeHandler<T> handler)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        TypeHandlerRegistry.Register<T>(new AdaptedTypeHandler<T>(handler));
    }

    /// <summary>
    /// Removes the registered type handler for the specified type.
    /// </summary>
    public static bool RemoveTypeHandler<T>() => TypeHandlerRegistry.Remove<T>();

        /// <summary>
    /// Gets the interceptor pipeline for command execution hooks.
    /// </summary>
    /// <remarks>
    /// Use <see cref="AddInterceptor(ICommandInterceptor)"/> or <see cref="AddInterceptors(IEnumerable{ICommandInterceptor})"/> to register interceptors.
    /// </remarks>
    public static InterceptorPipeline? InterceptorPipeline
    {
        get => _interceptorPipeline;
        private set => _interceptorPipeline = value;
    }

    /// <summary>
    /// Adds a single interceptor to the pipeline.
    /// </summary>
    /// <param name="interceptor">The interceptor to add.</param>
    /// <remarks>
    /// Interceptors are executed in registration order during command execution.
    /// </remarks>
    public static void AddInterceptor(ICommandInterceptor interceptor)
    {
        if (interceptor is null)
            throw new ArgumentNullException(nameof(interceptor));

        lock (InterceptorSync)
        {
            var existingInterceptors = _interceptorPipeline?.GetInterceptors() ?? Enumerable.Empty<ICommandInterceptor>();
            _interceptorPipeline = new InterceptorPipeline(existingInterceptors.Concat(new[] { interceptor }));
        }
    }

    /// <summary>
    /// Adds multiple interceptors to the pipeline.
    /// </summary>
    /// <param name="interceptors">The interceptors to add.</param>
    /// <remarks>
    /// Interceptors are executed in registration order during command execution.
    /// </remarks>
    public static void AddInterceptors(IEnumerable<ICommandInterceptor> interceptors)
    {
        if (interceptors is null)
            throw new ArgumentNullException(nameof(interceptors));

        lock (InterceptorSync)
        {
            var existingInterceptors = _interceptorPipeline?.GetInterceptors() ?? Enumerable.Empty<ICommandInterceptor>();
            _interceptorPipeline = new InterceptorPipeline(existingInterceptors.Concat(interceptors));
        }
    }

    /// <summary>
    /// Clears all registered interceptors.
    /// </summary>
    public static void ClearInterceptors()
    {
        lock (InterceptorSync)
        {
            _interceptorPipeline = null;
        }
    }

    /// <summary>
    /// Resets all configuration options to their default values.
    /// </summary>
    public static void Reset()
    {
        _schemaNameResolver = null;
        _tableNameResolver = null;
        _columnNameResolver = null;
        _logger = null;
        _reflectionMapperResolver = null;
        _specialTypeMapperResolver = null;
        _reflectionInsertBinderResolver = null;
        _reflectionUpdateBinderResolver = null;
        _reflectionDeleteBinderResolver = null;
        ReflectionTableMetadataResolver = null;
        ReflectionMultiMapperResolver = null;
        ReflectionMultiMapperResolverN = null;
        _interceptorPipeline = null;
        _defaultEnumStorage = EnumStorage.Numeric;
        _parameterParsingCapacity = 8;
        _queryResultCapacity = 64;
        _csvFieldCapacity = 16;
        TypeHandlerRegistry.Clear();
    }
    private sealed class AdaptedTypeHandler<T> : ITypeHandler
    {
        private readonly TypeHandler<T> _handler;
        internal AdaptedTypeHandler(TypeHandler<T> handler) => _handler = handler;
        object? ITypeHandler.Parse(object? dbValue) => _handler.Parse(dbValue);
        object? ITypeHandler.ToDbValue(object? value) => _handler.ToDbValue((T?)value);
    }

    private sealed class DelegateTypeHandler<T> : ITypeHandler
    {
        private readonly Func<object?, T> _fromDb;
        private readonly Func<T?, object?> _toDb;
        internal DelegateTypeHandler(Func<object?, T> fromDb, Func<T?, object?> toDb)
        {
            _fromDb = fromDb;
            _toDb = toDb;
        }
        object? ITypeHandler.Parse(object? dbValue) => _fromDb(dbValue);
        object? ITypeHandler.ToDbValue(object? value) => _toDb((T?)value);
    }
}
