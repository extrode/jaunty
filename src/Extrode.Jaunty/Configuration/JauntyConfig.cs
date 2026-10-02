
using System.ComponentModel;
using System.Data;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Interceptors;
using Extrode.Jaunty.Internals;
using Extrode.Jaunty.TypeHandlers;

using Extrode.Jaunty.Import;

namespace Extrode.Jaunty.Configuration;

/// <summary>
/// Provides global configuration options for Extrode.Jaunty's entity-to-database mapping behavior.
/// </summary>
/// <remarks>
/// <para><b>Set once, at startup.</b> Names, mapping hooks, enum storage and type handlers are set
/// through one <see cref="Configure"/> call and are read-only afterwards: their getters are here,
/// their setters are on <see cref="JauntyConfigBuilder"/>. One operation reads them more than once -
/// for its SQL text, then for its parameters or reader ordinals - so a change landing between those
/// reads would pair SQL built under one configuration with parameters built under another. See
/// docs/decisions/2026-10-02-014-configuration-is-set-once-at-startup.md and the upgrade guide,
/// docs/06-releases/upgrading-to-configure.md.</para>
/// <para><b>Still settable at runtime:</b> <see cref="Logger"/>, the capacity settings and the
/// interceptors. Each is read once per use, so no operation can see two values of one. The
/// interceptor mutation methods (<see cref="AddInterceptor"/>, <see cref="AddInterceptors"/>,
/// <see cref="ClearInterceptors"/>) are synchronized, so concurrent registration cannot lose
/// interceptors, and a command that is already executing keeps the pipeline it observed at its
/// start.</para>
/// <para><see cref="Reset"/> is for test cleanup only, and is <b>not</b> atomic as a whole.</para>
/// </remarks>
public static class JauntyConfig
{
    private static readonly object InterceptorSync = new();
    private static readonly object ConfigureSync = new();

    private const int Unconfigured = 0;
    private const int Configuring = 1;
    private const int Configured = 2;

    private static volatile JauntySettings _settings = JauntySettings.Defaults;
    private static int _state = Unconfigured;
    private static volatile int _configuringThread;
    private static int _autoInstallSkipped;

    private static volatile Action<string, object?>? _logger;
    private static volatile InterceptorPipeline? _interceptorPipeline;

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
    /// <remarks>
    /// The default is not raised for large reads, and there is no growth logic to keep an unhinted
    /// list under the large-object threshold: a caller reading thousands of rows passes
    /// <c>CommandOptions&lt;T&gt;.WithExpectedRowCount(n)</c>. Measured and decided 2026-09-02,
    /// docs/decisions/2026-09-02-012-result-list-default-capacity-stays-64.md.
    /// </remarks>
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
    /// Sets names, mapping hooks, enum storage and type handlers, once, for the life of the process.
    /// </summary>
    /// <param name="configure">Sets the settings on the builder it is given.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Extrode.Jaunty is already configured with different settings, or an operation has already
    /// read the settings (any query, any generated <c>TableName</c>, any Fluent query, or a read of a
    /// <see cref="JauntyConfig"/> mapping setting such as <see cref="DefaultEnumStorage"/>) before this call.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The builder starts from the defaults. If Extrode.Jaunty.Extensions.Reflection is present,
    /// reflection mapping is switched on first, exactly as it is without a <c>Configure</c> call;
    /// <paramref name="configure"/> then runs and wins over it. The result replaces the settings in
    /// one write.
    /// </para>
    /// <para>
    /// A second call with the same settings does nothing, so a test host that runs
    /// <c>Program.cs</c> again in the same process keeps working. Settings compare equal when every
    /// delegate refers to the same method on the same target - true of method groups and of lambdas
    /// that capture nothing; type handlers compare by their type only, so two instances of one
    /// handler class with different constructor arguments count as the same. A lambda that captures a value is
    /// a new delegate each time, so make that call with <see cref="TryConfigure"/> instead.
    /// </para>
    /// <para>
    /// A repeated call runs <paramref name="configure"/> again to compare its result, so keep the
    /// callback free of side effects.
    /// </para>
    /// </remarks>
    public static void Configure(Action<JauntyConfigBuilder> configure)
        => ConfigureCore(configure, compareWhenConfigured: true);

    /// <summary>
    /// Sets the mapping settings unless they are already set. The first call in a process applies
    /// <paramref name="configure"/> exactly as <see cref="Configure"/> does and returns
    /// <see langword="true"/>; every later call returns <see langword="false"/> without running it.
    /// </summary>
    /// <remarks>
    /// Use it where the same startup code can run more than once in one process with settings that
    /// compare unequal, such as test hosts started in parallel whose resolver captures a value read
    /// from configuration. A call racing another thread's <see cref="Configure"/> waits for it to
    /// finish. Like <see cref="Configure"/>, it throws once an operation has read the settings.
    /// </remarks>
    /// <param name="configure">Sets the frozen settings on the builder.</param>
    /// <returns>Whether this call applied the settings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The settings are not configured yet and an operation has already read them.
    /// </exception>
    public static bool TryConfigure(Action<JauntyConfigBuilder> configure)
        => ConfigureCore(configure, compareWhenConfigured: false);

    private static bool ConfigureCore(Action<JauntyConfigBuilder> configure, bool compareWhenConfigured)
    {
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        SpinWait spin = default;
        while (true)
        {
            int prior = Interlocked.CompareExchange(ref _state, Configuring, Unconfigured);
            if (prior == Unconfigured)
                break;

            if (prior == Configuring)
            {
                if (_configuringThread == Environment.CurrentManagedThreadId)
                    throw new InvalidOperationException("JauntyConfig.Configure was called from inside its own callback.");
                spin.SpinOnce();
                continue;
            }

            if (!compareWhenConfigured || Build(configure).SameAs(_settings))
                return false;

            throw new InvalidOperationException(
                "Extrode.Jaunty is already configured with different settings. JauntyConfig.Configure " +
                "sets them once per process; make a call that can repeat with different settings " +
                "through JauntyConfig.TryConfigure(...). " +
                "See docs/06-releases/upgrading-to-configure.md.");
        }

        _configuringThread = Environment.CurrentManagedThreadId;
        try
        {
            if (ConfigurationGeneration.HasBeenRead)
                throw AlreadyRead();

            JauntySettings previous = _settings;
            Apply(Build(configure), clearHandlers: true);

            // A first read racing this call marks the flag before it loads the settings, and the
            // barrier orders the publish above before the check below, so a race is always detected:
            // either that read saw only the new settings or this check sees the flag. Detection is all
            // it is - an operation that reads more than once may still see the new settings between
            // this publish and the rollback below, so it can fail while this call throws.
            Interlocked.MemoryBarrier();
            if (ConfigurationGeneration.HasBeenRead)
            {
                Apply(previous, clearHandlers: true);
                throw AlreadyRead();
            }

            _configuringThread = 0;
            Interlocked.Exchange(ref _autoInstallSkipped, 0);
            Volatile.Write(ref _state, Configured);
            return true;
        }
        catch
        {
            _configuringThread = 0;
            Volatile.Write(ref _state, Unconfigured);
            if (Interlocked.Exchange(ref _autoInstallSkipped, 0) == 1)
                Jaunty.TryEnableReflectionMapping();
            throw;
        }
    }

    private static InvalidOperationException AlreadyRead() => new InvalidOperationException(
        "JauntyConfig.Configure was called after Extrode.Jaunty had already read its " +
        "settings (a query, a generated TableName, a Fluent query, or a read of a JauntyConfig " +
        "mapping setting ran first). Call it " +
        "at startup, before anything else touches Extrode.Jaunty. " +
        "See docs/06-releases/upgrading-to-configure.md.");

    /// <summary>
    /// Whether <see cref="Configure"/> has completed. Use it to guard a call that can run more than
    /// once in one process with settings that compare unequal, such as a lambda capturing a value
    /// read from configuration.
    /// </summary>
    public static bool IsConfigured => Volatile.Read(ref _state) == Configured;

    /// <summary>
    /// Drops a type handler from the current settings after <c>TypeHandlerRegistry.Remove</c> took
    /// it out of the registry, so the two stay in step.
    /// </summary>
    internal static void ForgetTypeHandler(Type key)
    {
        lock (ConfigureSync)
        {
            var builder = new JauntyConfigBuilder(_settings);
            builder.RemoveTypeHandlers(key);
            _settings = new JauntySettings(builder);
        }
    }

    private static JauntySettings Build(Action<JauntyConfigBuilder> configure)
    {
        var builder = new JauntyConfigBuilder();
        AutoReflection.Apply(builder);
        configure(builder);
        return new JauntySettings(builder);
    }

    /// <summary>
    /// Replaces the settings, registers their type handlers and retires every derived cache.
    /// </summary>
    private static void Apply(JauntySettings settings, bool clearHandlers, int firstNewHandler = 0)
    {
        lock (ConfigureSync)
        {
            _settings = settings;
            if (clearHandlers)
                TypeHandlerRegistry.Clear();
            for (int i = firstNewHandler; i < settings.TypeHandlers.Length; i++)
                TypeHandlerRegistry.Register(settings.TypeHandlers[i].Key, settings.TypeHandlers[i].Value);
        }

        ConfigurationGeneration.Invalidate();
    }

    /// <summary>
    /// Changes the current settings in place, at any time, without the once-only and read-first
    /// checks. For Extrode.Jaunty's own tests, which set one resolver at a time across a process
    /// that has long since run queries; never called by the library.
    /// </summary>
    internal static void Reconfigure(Action<JauntyConfigBuilder> change)
    {
        if (change is null)
            throw new ArgumentNullException(nameof(change));

        lock (ConfigureSync)
        {
            var builder = new JauntyConfigBuilder(_settings);
            change(builder);
            Apply(new JauntySettings(builder), clearHandlers: false, firstNewHandler: builder.AddedTypeHandlersFrom);
        }
    }

    /// <summary>
    /// Installs reflection mapping when <see cref="Configure"/> has not been called, filling only
    /// the hooks that are still unset. Called from <c>Jaunty</c>'s static constructor, which runs on
    /// the first touch of the <c>Jaunty</c> class - possibly after generated code or Fluent has
    /// already read the settings, so this bypasses both the once-only rule and the read-first check.
    /// It leaves <see cref="IsConfigured"/> false.
    /// </summary>
    /// <returns>The unexpected exception the probe swallowed, or <see langword="null"/>.</returns>
    internal static Exception? InstallAutoReflectionIfUnconfigured(Func<System.Reflection.AssemblyName, System.Reflection.Assembly> load)
    {
        int prior = Interlocked.CompareExchange(ref _state, Configuring, Unconfigured);
        if (prior != Unconfigured)
        {
            // A Configure in flight installs reflection itself; if it fails instead, its catch
            // sees this flag and retries the install.
            if (prior == Configuring)
                Interlocked.Exchange(ref _autoInstallSkipped, 1);
            return null;
        }

        try
        {
            var probed = new JauntyConfigBuilder();
            Exception? failure = AutoReflection.TryApply(probed, load);

            lock (ConfigureSync)
            {
                var merged = new JauntyConfigBuilder(_settings);
                merged.ReflectionMapperResolver ??= probed.ReflectionMapperResolver;
                merged.ReflectionInsertBinderResolver ??= probed.ReflectionInsertBinderResolver;
                merged.ReflectionUpdateBinderResolver ??= probed.ReflectionUpdateBinderResolver;
                merged.ReflectionDeleteBinderResolver ??= probed.ReflectionDeleteBinderResolver;
                merged.ReflectionTableMetadataResolver ??= probed.ReflectionTableMetadataResolver;
                merged.ReflectionMultiMapperResolver ??= probed.ReflectionMultiMapperResolver;
                merged.ReflectionMultiMapperResolverN ??= probed.ReflectionMultiMapperResolverN;
                merged.SpecialTypeMapperResolver ??= probed.SpecialTypeMapperResolver;
                Apply(new JauntySettings(merged), clearHandlers: false, firstNewHandler: merged.AddedTypeHandlersFrom);
            }
            return failure;
        }
        finally
        {
            Volatile.Write(ref _state, Unconfigured);
        }
    }

    private static JauntySettings Read()
    {
        ConfigurationGeneration.MarkRead();
        return _settings;
    }

    /// <summary>Resolves an entity type's schema name. Set through <see cref="Configure"/>.</summary>
    /// <remarks><inheritdoc cref="JauntyConfigBuilder.SchemaNameResolver" path="/remarks"/></remarks>
    public static Func<Type, string>? SchemaNameResolver => Read().SchemaNameResolver;

    /// <summary>Resolves an entity type's table name. Set through <see cref="Configure"/>.</summary>
    /// <remarks><inheritdoc cref="JauntyConfigBuilder.TableNameResolver" path="/remarks"/></remarks>
    public static Func<Type, string>? TableNameResolver => Read().TableNameResolver;

    /// <summary>Resolves a property's column name. Set through <see cref="Configure"/>.</summary>
    /// <remarks><inheritdoc cref="JauntyConfigBuilder.ColumnNameResolver" path="/remarks"/></remarks>
    public static Func<string, string>? ColumnNameResolver => Read().ColumnNameResolver;

    /// <inheritdoc cref="JauntyConfigBuilder.ReflectionMapperResolver"/>
    public static Func<Type, MappingMode, object>? ReflectionMapperResolver => Read().ReflectionMapperResolver;

    /// <inheritdoc cref="JauntyConfigBuilder.ReflectionInsertBinderResolver"/>
    public static Func<Type, Action<IDbCommand, object>>? ReflectionInsertBinderResolver => Read().ReflectionInsertBinderResolver;

    /// <inheritdoc cref="JauntyConfigBuilder.ReflectionUpdateBinderResolver"/>
    public static Func<Type, Action<IDbCommand, object>>? ReflectionUpdateBinderResolver => Read().ReflectionUpdateBinderResolver;

    /// <inheritdoc cref="JauntyConfigBuilder.ReflectionDeleteBinderResolver"/>
    public static Func<Type, Action<IDbCommand, object>>? ReflectionDeleteBinderResolver => Read().ReflectionDeleteBinderResolver;

    /// <inheritdoc cref="JauntyConfigBuilder.ReflectionTableMetadataResolver"/>
    public static Func<Type, object>? ReflectionTableMetadataResolver => Read().ReflectionTableMetadataResolver;

    /// <inheritdoc cref="JauntyConfigBuilder.CopyImportFactory"/>
    public static CopyImportFactory? CopyImportFactory => Read().CopyImportFactory;

    /// <inheritdoc cref="JauntyConfigBuilder.ReflectionMultiMapperResolver"/>
    public static Func<Type, Type, object>? ReflectionMultiMapperResolver => Read().ReflectionMultiMapperResolver;

    /// <inheritdoc cref="JauntyConfigBuilder.ReflectionMultiMapperResolverN"/>
    public static Func<Type[], IDataReader, Action<object, IDataRecord>[]>? ReflectionMultiMapperResolverN => Read().ReflectionMultiMapperResolverN;

    /// <inheritdoc cref="JauntyConfigBuilder.SpecialTypeMapperResolver"/>
    public static Func<Type, IDataReader, object>? SpecialTypeMapperResolver => Read().SpecialTypeMapperResolver;

    /// <inheritdoc cref="JauntyConfigBuilder.DefaultEnumStorage"/>
    public static EnumStorage DefaultEnumStorage => Read().DefaultEnumStorage;

    /// <summary>
    /// Gets or sets a global diagnostic logger for SQL commands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This callback performs no redaction.</b> The second argument is the caller's parameter
    /// object exactly as supplied - passwords, tokens and keys included. Extrode.Jaunty masks sensitive
    /// values only in <c>LoggingInterceptor</c>, which applies
    /// <c>LoggingConfiguration.SensitiveParameterNames</c> (both in the optional
    /// Extrode.Jaunty.Extensions.Logging package); nothing in that path runs before
    /// this delegate. A handler that writes the object to a log, a file or a telemetry sink is
    /// responsible for its own redaction.
    /// </para>
    /// <para>
    /// AUD-R26 (batch 4, medium/security). This was a one-line summary with no such warning, next to
    /// a masking feature that made it reasonable to assume Extrode.Jaunty redacted globally. It does not, and
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
    /// Restores every setting to its default and allows <see cref="Configure"/> to run again.
    /// <b>For test cleanup only.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Clears the frozen settings and type handlers, the logger, the capacities, the interceptors,
    /// <see cref="BulkCopyConfiguration"/> and custom dialect registrations, then retires every
    /// derived cache. It also clears the record that the settings were read, so a test can call
    /// <see cref="Configure"/> afterwards.
    /// </para>
    /// <para>
    /// Not atomic as a whole, and it races any operation running at the same time: a test that
    /// calls <c>Reset()</c> then <c>Configure</c> must run in a collection that does not run in
    /// parallel with other tests using Extrode.Jaunty. It does not switch reflection mapping back
    /// on; <see cref="Configure"/> does, or the extension's <c>UseReflectionMapping()</c> inside it.
    /// </para>
    /// <para>
    /// AUD-R26 (batch 4): bumping <see cref="Internals.ConfigurationGeneration"/> retires the
    /// entries <c>WriteParameterCache&lt;T&gt;</c>, <c>CrudSqlCache</c> and the other derived caches
    /// built before the reset, so the next lookup rebuilds from the settings as they stand then. It
    /// stays last so that nothing rebuilt between the first write and this line survives either.
    /// </para>
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void Reset()
    {
        SpinWait spin = default;
        while (true)
        {
            int state = Volatile.Read(ref _state);
            if (state == Configuring)
            {
                if (_configuringThread == Environment.CurrentManagedThreadId)
                    throw new InvalidOperationException("JauntyConfig.Reset was called from inside a Configure callback.");
                spin.SpinOnce();
                continue;
            }
            if (Interlocked.CompareExchange(ref _state, Configuring, state) == state)
                break;
        }

        try
        {
            lock (ConfigureSync)
            {
                _settings = JauntySettings.Defaults;
                TypeHandlerRegistry.Clear();
                Interlocked.Exchange(ref _autoInstallSkipped, 0);
            }

            _logger = null;
            lock (InterceptorSync)
            {
                _interceptorPipeline = null;
            }
            _parameterParsingCapacity = 8;
            _queryResultCapacity = 64;
            _csvFieldCapacity = 16;
            BulkCopyConfiguration.Reset();
            Dialects.SqlDialectFactory.ResetRegistrations();

            ConfigurationGeneration.Invalidate();
            ConfigurationGeneration.ClearRead();
        }
        finally
        {
            // Released last, so a Configure waiting on Reset never sees the read flag Reset is about to clear.
            Volatile.Write(ref _state, Unconfigured);
        }
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
}
