
using System.Data;

using Jaunty.Attributes;
using Jaunty.Interceptors;
using Jaunty.Internals;
using Jaunty.TypeHandlers;

using Jaunty.Import;

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
    private static volatile CopyImportFactory? _copyImportFactory;
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
        set { _schemaNameResolver = value; ConfigurationGeneration.Invalidate(); }
    }

    /// <summary>
    /// Gets or sets a custom resolver for table names.
    /// </summary>
    public static Func<Type, string>? TableNameResolver
    {
        get => _tableNameResolver;
        set { _tableNameResolver = value; ConfigurationGeneration.Invalidate(); }
    }

    /// <summary>
    /// Gets or sets a custom resolver for column names.
    /// </summary>
    public static Func<string, string>? ColumnNameResolver
    {
        get => _columnNameResolver;
        set { _columnNameResolver = value; ConfigurationGeneration.Invalidate(); }
    }

    /// <summary>
    /// Gets or sets a global diagnostic logger for SQL commands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This callback performs no redaction.</b> The second argument is the caller's parameter
    /// object exactly as supplied - passwords, tokens and keys included. Jaunty masks sensitive
    /// values only in <c>LoggingInterceptor</c>, which applies
    /// <c>LoggingConfiguration.SensitiveParameterNames</c> (both in the optional
    /// Extrode.Jaunty.Extensions.Logging package); nothing in that path runs before
    /// this delegate. A handler that writes the object to a log, a file or a telemetry sink is
    /// responsible for its own redaction.
    /// </para>
    /// <para>
    /// AUD-R26 (batch 4, medium/security). This was a one-line summary with no such warning, next to
    /// a masking feature that made it reasonable to assume Jaunty redacted globally. It does not, and
    /// this is the hook people reach for first because it needs no dependency injection.
    /// <see cref="Interceptors.CommandContext.Parameters"/> already carried the equivalent warning;
    /// this one did not. Register a <c>LoggingInterceptor</c> instead if you want
    /// masking, or call <c>LoggingConfiguration.IsSensitiveParameter</c> from your handler; both
    /// are in the optional Extrode.Jaunty.Extensions.Logging package.
    /// </para>
    /// </remarks>
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
        set { _reflectionMapperResolver = value; ConfigurationGeneration.Invalidate(); }
    }

    /// <summary>
    /// Optional fallback parameter binder resolver for INSERT operations.
    /// </summary>
    public static Func<Type, Action<IDbCommand, object>>? ReflectionInsertBinderResolver
    {
        get => _reflectionInsertBinderResolver;
        set { _reflectionInsertBinderResolver = value; ConfigurationGeneration.Invalidate(); }
    }

    /// <summary>
    /// Optional fallback parameter binder resolver for UPDATE operations.
    /// </summary>
    public static Func<Type, Action<IDbCommand, object>>? ReflectionUpdateBinderResolver
    {
        get => _reflectionUpdateBinderResolver;
        set { _reflectionUpdateBinderResolver = value; ConfigurationGeneration.Invalidate(); }
    }

    /// <summary>
    /// Optional fallback parameter binder resolver for DELETE operations.
    /// </summary>
    public static Func<Type, Action<IDbCommand, object>>? ReflectionDeleteBinderResolver
    {
        get => _reflectionDeleteBinderResolver;
        set { _reflectionDeleteBinderResolver = value; ConfigurationGeneration.Invalidate(); }
    }

    /// <summary>
    /// Optional fallback table metadata resolver for types that are not source-generated.
    /// Returns an EntityMetadata object.
    /// </summary>
    public static Func<Type, object>? ReflectionTableMetadataResolver
    {
        get => _reflectionTableMetadataResolver;
        set { _reflectionTableMetadataResolver = value; ConfigurationGeneration.Invalidate(); }
    }

    /// <summary>
    /// Optional client-side bulk-copy provider, used by CSV import for the streaming
    /// <c>COPY ... FROM STDIN</c> path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Core declares no dependency on a database driver, so it cannot call a provider's copy API
    /// directly. Until this hook it called PostgreSQL's by reflection - five
    /// <c>GetType().GetMethod(...)</c> probes that all returned null under trimming, one of which
    /// then committed a partially written import instead of aborting it.
    /// </para>
    /// <para>
    /// Install <c>Extrode.Jaunty.Extensions.Npgsql</c> and call <c>JauntyNpgsql.Use()</c> for
    /// PostgreSQL, or assign your own <see cref="CopyImportFactory"/> for another driver. Leave it
    /// unset and CSV import uses the server-side path, where the database engine opens the file.
    /// </para>
    /// </remarks>
    public static CopyImportFactory? CopyImportFactory
    {
        get => _copyImportFactory;
        set { _copyImportFactory = value; ConfigurationGeneration.Invalidate(); }
    }

    /// <summary>
    /// Optional fallback multi-mapper resolver for types that are not source-generated.
    /// Returns a MultiEntityMapper object.
    /// </summary>
    public static Func<Type, Type, object>? ReflectionMultiMapperResolver
    {
        get => _reflectionMultiMapperResolver;
        set { _reflectionMultiMapperResolver = value; ConfigurationGeneration.Invalidate(); }
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
        set { _reflectionMultiMapperResolverN = value; ConfigurationGeneration.Invalidate(); }
    }


    /// <summary>
    /// Optional resolver for special types (Dictionary, KeyValuePair, ValueTuple, ExpandoObject).
    /// Typically provided by Jaunty.Extensions.Reflection.SpecialTypeMappers.Register().
    /// Returns a Func&lt;IDataReader, object&gt; that creates the special type instance.
    /// </summary>
    public static Func<Type, IDataReader, object>? SpecialTypeMapperResolver
    {
        get => _specialTypeMapperResolver;
        set { _specialTypeMapperResolver = value; ConfigurationGeneration.Invalidate(); }
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
    /// Adds a single interceptor to the pipeline, unless a reference-equal instance is already
    /// registered.
    /// </summary>
    /// <param name="interceptor">The interceptor to add.</param>
    /// <returns><see langword="true"/> if the interceptor was added; <see langword="false"/> if a
    /// reference-equal instance was already present.</returns>
    /// <remarks>
    /// Unlike checking the pipeline's interceptors then calling <see cref="AddInterceptor"/>
    /// separately, the presence check and the add happen atomically under the same lock, so
    /// concurrent callers cannot both observe "not yet registered" and both append the same
    /// instance.
    /// </remarks>
    public static bool AddInterceptorIfNotPresent(ICommandInterceptor interceptor)
    {
        if (interceptor is null)
            throw new ArgumentNullException(nameof(interceptor));

        lock (InterceptorSync)
        {
            var existingInterceptors = _interceptorPipeline?.GetInterceptors() ?? Enumerable.Empty<ICommandInterceptor>();
            foreach (ICommandInterceptor existing in existingInterceptors)
            {
                if (ReferenceEquals(existing, interceptor))
                    return false;
            }

            _interceptorPipeline = new InterceptorPipeline(existingInterceptors.Concat(new[] { interceptor }));
            return true;
        }
    }

    /// <summary>
    /// Adds multiple interceptors to the pipeline.
    /// </summary>
    /// <param name="interceptors">The interceptors to add.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="interceptors"/> is <see langword="null"/>, or any element of it is.
    /// </exception>
    /// <remarks>
    /// Interceptors are executed in registration order during command execution.
    /// <para>
    /// AUD-R35-142. The sequence was checked and its elements were not, while the singular
    /// <see cref="AddInterceptor"/> rejects a null interceptor outright. A null element went into
    /// the pipeline's array - <see cref="InterceptorPipeline"/>'s constructor guards the sequence
    /// only - and surfaced as a <see cref="NullReferenceException"/> from inside command execution,
    /// with nothing in the stack pointing back at the registration that put it there. The sequence
    /// is materialised once so that a lazy one cannot yield different elements to the check and to
    /// the pipeline.
    /// </para>
    /// </remarks>
    public static void AddInterceptors(IEnumerable<ICommandInterceptor> interceptors)
    {
        if (interceptors is null)
            throw new ArgumentNullException(nameof(interceptors));

        var added = new List<ICommandInterceptor>();
        foreach (ICommandInterceptor interceptor in interceptors)
        {
            if (interceptor is null)
                throw new ArgumentNullException(nameof(interceptors), "The interceptor sequence contains a null element.");

            added.Add(interceptor);
        }

        lock (InterceptorSync)
        {
            var existingInterceptors = _interceptorPipeline?.GetInterceptors() ?? Enumerable.Empty<ICommandInterceptor>();
            _interceptorPipeline = new InterceptorPipeline(existingInterceptors.Concat(added));
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
    /// <remarks>
    /// <para>
    /// AUD-R26 (batch 4, medium/bug). This used to clear the resolver fields and stop there, which
    /// left the write path unrecoverable: <c>WriteParameterCache&lt;T&gt;</c> and every other cache
    /// derived from configuration kept whatever they had built before the reset, so re-registering
    /// a resolver afterwards changed nothing. Bumping
    /// <see cref="Internals.ConfigurationGeneration"/> retires those entries; the next lookup of
    /// each rebuilds from the configuration as it stands then.
    /// </para>
    /// <para>
    /// AUD-R26 (batch 4, low/consistency). "All" now means all. Two further process-wide surfaces
    /// used to survive this call, both of them public and both mutable from a test:
    /// <see cref="BulkCopyConfiguration"/>'s six settable statics, which had their own
    /// <see cref="BulkCopyConfiguration.Reset"/> that this method never called, and
    /// <c>SqlDialectFactory</c>'s custom registrations, which had no reset at all - so a dialect
    /// registered for a connection type name in one test governed every connection of that name for
    /// the rest of the process. Given this method's stated purpose is test cleanup, a reader
    /// reasonably concludes one call restores a clean slate, and now one does.
    /// </para>
    /// <para>
    /// <see cref="DefaultEnumStorage"/> and the capacity settings do not participate in the
    /// generation counter: nothing compiles them in, every consumer re-reads them per call by
    /// design (see <c>MetadataCache&lt;T&gt;.CreateFallbackSetter</c> and
    /// <c>JauntyReflectionExtensions.BuildValueConverter</c>), so invalidating on them would only
    /// discard work that is still correct.
    /// </para>
    /// </remarks>
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
        CopyImportFactory = null;
        ReflectionMultiMapperResolver = null;
        ReflectionMultiMapperResolverN = null;
        lock (InterceptorSync)
        {
            _interceptorPipeline = null;
        }
        _defaultEnumStorage = EnumStorage.Numeric;
        _parameterParsingCapacity = 8;
        _queryResultCapacity = 64;
        _csvFieldCapacity = 16;
        TypeHandlerRegistry.Clear();
        BulkCopyConfiguration.Reset();
        Dialects.SqlDialectFactory.ResetRegistrations();

        // AUD-R35-141: this call is the only thing that retires the caches, and the comment that
        // used to sit here said the opposite - that "the individual field writes above each bump the
        // generation already" and this was a top-up for TypeHandlerRegistry and the two surfaces
        // below. They do not: nine of the twelve resolver resets are direct writes to the backing
        // fields, which bypass the property setters and their Invalidate() calls, and only the three
        // written through properties bump anything. So a future edit that trusted the old comment and
        // dropped this line would have silently reinstated AUD-R26's stale-cache bug - CrudSqlCache,
        // MultiRowInsertCache, MetadataCache<T> and WriteParameterCache<T>'s bindings all keep
        // serving entries built from the configuration Reset() just cleared. It stays last so that
        // nothing rebuilt between the first field write and this line survives either.
        ConfigurationGeneration.Invalidate();
    }

    /// <summary>
    /// Atomically captures the currently registered interceptors and clears the pipeline, for
    /// callers (notably tests) that need to snapshot-then-restore interceptor state around a
    /// <see cref="Reset"/> call without losing interceptors registered concurrently by other
    /// threads between the snapshot and the clear.
    /// </summary>
    internal static ICommandInterceptor[]? CaptureAndClearInterceptors()
    {
        lock (InterceptorSync)
        {
            var existing = _interceptorPipeline?.GetInterceptors().ToArray();
            _interceptorPipeline = null;
            return existing;
        }
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
