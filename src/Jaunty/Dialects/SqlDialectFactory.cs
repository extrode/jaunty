using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Jaunty.Dialects;

/// <summary>
/// Factory for creating SQL dialects based on connection type.
/// Auto-detects the database provider from the connection object.
/// Dialect instances are cached per connection type for zero-allocation lookups.
/// </summary>
public static class SqlDialectFactory
{
    private static readonly ConcurrentDictionary<Type, ISqlDialect> _dialectCache = new();
    private static readonly ConcurrentDictionary<string, ISqlDialect> _customDialects = new();

    /// <summary>
    /// Gets the SQL dialect for the specified database connection.
    /// </summary>
    /// <param name="connection">The database connection.</param>
    /// <returns>The SQL dialect instance for the connection type.</returns>
    /// <exception cref="InvalidOperationException">
    /// No dialect is registered for the connection type and no connection could be found underneath
    /// it. The message names the type and how to register one.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Resolution order: a dialect registered for the connection's <c>Type.Name</c>, then the
    /// built-in names (<c>SqlConnection</c>, <c>NpgsqlConnection</c>, <c>MySqlConnection</c>,
    /// <c>SQLiteConnection</c>, <c>SqliteConnection</c>), then the connection a decorator wraps, and
    /// otherwise a throw. Dialects are cached per connection type for optimal performance.
    /// </para>
    /// <para>
    /// <b>Behaviour change, AUD-R26 (batch 4).</b> An unrecognised connection type used to receive
    /// <see cref="SqlServerDialect"/> silently - <c>[bracket]</c> quoting, <c>MERGE</c>-based
    /// upsert, <c>SCOPE_IDENTITY()</c>, <c>OFFSET ... FETCH NEXT</c> paging and a 2,100-parameter
    /// ceiling, on an engine that need not support any of them - and this documentation said only
    /// that the provider was auto-detected, with no mention of a fallback. It now throws instead,
    /// which turns wrong SQL produced quietly into an error naming the type.
    /// </para>
    /// <para>
    /// Because resolution keys on the type <em>name</em>, the most common casualty was never an
    /// exotic engine but a wrapped one - MiniProfiler's <c>ProfiledDbConnection</c>, an
    /// OpenTelemetry decorator, a DI proxy - so a decorated <c>SqliteConnection</c> produced SQL
    /// Server SQL. Those now resolve correctly, by looking through the decorator to the connection
    /// underneath. Anything still unrecognised is a
    /// <see cref="RegisterDialect(string, ISqlDialect)"/> call away.
    /// </para>
    /// </remarks>
    public static ISqlDialect GetDialect(IDbConnection connection)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
#endif

        return GetDialectCore(connection, depth: 0);
    }

    // R27 batch 7: the decorator recursion had no depth bound - ReferenceEquals catches a
    // decorator returning itself, but a two-element cycle (A exposes B, B exposes A) recursed
    // until the stack overflowed. Bounded like Unwrap's MaxDepth, and for the same reason.
    private const int MaxDecoratorDepth = 8;

    private static ISqlDialect GetDialectCore(IDbConnection connection, int depth)
    {
        Type connectionType = connection.GetType();
        if (_dialectCache.TryGetValue(connectionType, out ISqlDialect? cached))
            return cached;

        if (TryResolveByTypeName(connectionType.Name, out ISqlDialect? resolved))
        {
            _dialectCache.TryAdd(connectionType, resolved!);
            return resolved!;
        }

        // Not a name we know. Before giving up, look through connection decorators - see
        // TryGetInnerConnection. Deliberately NOT cached against the outer type: one wrapper type
        // wraps different engines in different places, so caching ProfiledDbConnection ->
        // SQLiteDialect would hand SQLite's dialect to a profiled Npgsql connection. The recursive
        // call caches against the inner type, which is the part worth caching.
        if (depth < MaxDecoratorDepth && TryGetInnerConnection(connection, out IDbConnection? inner))
            return GetDialectCore(inner!, depth + 1);

        throw new InvalidOperationException(
            $"No SQL dialect is registered for connection type '{connectionType.Name}'. " +
            "Jaunty resolves dialects from the connection type name and recognises SqlConnection, " +
            "NpgsqlConnection, MySqlConnection, SQLiteConnection and SqliteConnection. If this is a " +
            "wrapped or profiled connection, Jaunty could not reach the connection underneath it. " +
            $"Register a dialect for it with SqlDialectFactory.RegisterDialect(\"{connectionType.Name}\", dialect).");
    }

    /// <summary>
    /// Registers a custom SQL dialect for a specific connection type name.
    /// Custom registrations take priority over built-in dialect resolution.
    /// </summary>
    /// <param name="connectionTypeName">The Type.Name of the connection class (e.g., "DuckDBConnection").</param>
    /// <param name="dialect">The dialect instance to use for that connection type.</param>
    public static void RegisterDialect(string connectionTypeName, ISqlDialect dialect)
    {
        // R27 batch 7: a null registration used to be stored and surface later as a
        // NullReferenceException at an unrelated GetDialect call site.
        if (dialect is null) throw new ArgumentNullException(nameof(dialect));

        _customDialects[connectionTypeName] = dialect;

        // Invalidate any already-cached resolution for a connection type with this name so a
        // type resolved (and cached) via GetDialect before this registration doesn't keep
        // returning the stale built-in dialect on subsequent calls.
        foreach (Type cachedType in _dialectCache.Keys)
        {
            if (cachedType.Name == connectionTypeName)
                _dialectCache.TryRemove(cachedType, out _);
        }
    }

    /// <summary>
    /// Drops every resolved-and-cached dialect so the next <see cref="GetDialect"/> re-runs
    /// resolution, including the bulk-copy enhancement step.
    /// </summary>
    /// <remarks>
    /// AUD-R26. <c>UseNativeBulkCopy()</c> only flips a flag that
    /// <see cref="GetDialect"/> consults during resolution, and resolutions are cached per
    /// connection type. So if anything had already resolved a dialect for that connection type -
    /// one prior query is enough - the cache kept handing back the un-enhanced dialect and
    /// <c>UseNativeBulkCopy()</c> silently did nothing, permanently, for the life of the process.
    /// Bulk inserts then quietly took the loop path: correct results, far slower, and no way to
    /// tell from the outside.
    /// <para>
    /// <see cref="RegisterDialect(string, ISqlDialect)"/> already invalidated for exactly this
    /// reason; enabling bulk copy needed the same treatment and did not have it.
    /// </para>
    /// </remarks>
    internal static void InvalidateResolvedDialects() => _dialectCache.Clear();

    /// <summary>
    /// Drops every custom dialect registration along with the resolved-dialect cache, returning
    /// resolution to the built-in names alone.
    /// </summary>
    /// <remarks>
    /// AUD-R26 (batch 4, low/consistency). <c>_customDialects</c> is populated by the public
    /// <see cref="RegisterDialect(string, ISqlDialect)"/> overloads and had no reset of any kind, so
    /// a dialect registered for a connection type name in one test governed every connection of that
    /// name for the remainder of the process. <c>JauntyConfig.Reset()</c> - documented as resetting
    /// "all configuration options" and intended for test cleanup - now calls this, so one call
    /// really does restore a clean slate.
    /// <para>
    /// The registration order matters: the custom map is cleared first, so a resolution racing this
    /// call can repopulate the resolved cache from the built-ins but never from a registration that
    /// has already been dropped.
    /// </para>
    /// </remarks>
    internal static void ResetRegistrations()
    {
        _customDialects.Clear();
        _dialectCache.Clear();
    }

    /// <summary>
    /// Registers a custom SQL dialect for a specific connection type.
    /// Custom registrations take priority over built-in dialect resolution.
    /// </summary>
    /// <typeparam name="TConnection">The connection type to register the dialect for.</typeparam>
    /// <param name="dialect">The dialect instance to use for that connection type.</param>
    public static void RegisterDialect<TConnection>(ISqlDialect dialect) where TConnection : IDbConnection
    {
        if (dialect is null) throw new ArgumentNullException(nameof(dialect));

        var typeName = typeof(TConnection).Name;
        _customDialects[typeName] = dialect;

        // AUD-R26: this wrote the raw dialect straight into the resolution cache, skipping the
        // bulk-copy enhancement step every built-in resolution goes through. Enhancement is a
        // pass-through for anything that is not one of the four built-in dialects, so this is a
        // no-op for a genuinely custom dialect and correct for one derived from a built-in.
        _dialectCache[typeof(TConnection)] = TryEnhanceWithBulkCopy(dialect);
    }

    /// <summary>
    /// Returns the underlying engine dialect behind any <see cref="IDialectWrapper"/> decorations,
    /// or <paramref name="dialect"/> itself when it is not a decorator.
    /// </summary>
    /// <param name="dialect">The dialect to unwrap.</param>
    /// <returns>The innermost non-decorating dialect.</returns>
    /// <remarks>
    /// <para>
    /// Call this before any <c>dialect is SQLiteDialect</c>-style engine test on a dialect obtained
    /// from <see cref="GetDialect(IDbConnection)"/>. That method runs every dialect through the
    /// optional bulk-copy enhancement step, which - once <c>UseNativeBulkCopy()</c> has been called -
    /// substitutes a wrapper that implements <see cref="ISqlDialect"/> and delegates to the original
    /// rather than deriving from it, so a direct type test silently stops matching for every engine.
    /// </para>
    /// <para>
    /// Decorations may nest; this unwraps all of them. A self-referential or cyclic
    /// <see cref="IDialectWrapper.InnerDialect"/> chain is bounded rather than looped forever, and
    /// the deepest dialect reached is returned.
    /// </para>
    /// </remarks>
    public static ISqlDialect Unwrap(ISqlDialect dialect)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(dialect);
#else
        if (dialect is null) throw new ArgumentNullException(nameof(dialect));
#endif

        // Bounded rather than `while (true)`: InnerDialect is implemented by third-party dialects
        // too, and a wrapper that returns itself (or a cycle between two wrappers) would otherwise
        // hang the caller instead of degrading to "couldn't unwrap further".
        const int MaxDepth = 8;
        for (int i = 0; i < MaxDepth; i++)
        {
            if (dialect is not IDialectWrapper wrapper)
                return dialect;

            ISqlDialect inner = wrapper.InnerDialect;
            if (inner is null || ReferenceEquals(inner, dialect))
                return dialect;

            dialect = inner;
        }

        return dialect;
    }

    /// <summary>
    /// Resolves a dialect from a connection type name, or reports that the name is unknown.
    /// </summary>
    /// <remarks>
    /// AUD-R26 (batch 4, medium/bug). The final arm used to be
    /// <c>_ =&gt; new SqlServerDialect() // Default to SQL Server</c>, so any connection type Jaunty
    /// did not recognise silently received <c>[bracket]</c> quoting, <c>MERGE</c>-based upsert,
    /// <c>SCOPE_IDENTITY()</c>, <c>OFFSET ... FETCH NEXT</c> paging and a 2,100-parameter ceiling -
    /// wrong SQL produced quietly instead of an error the caller could act on, and
    /// <c>GetDialect</c>'s documentation never mentioned a fallback. Returning a miss instead lets
    /// <see cref="GetDialect"/> try unwrapping first and then fail with a message that names the
    /// type.
    /// </remarks>
    private static bool TryResolveByTypeName(string connectionTypeName, out ISqlDialect? dialect)
    {
        if (_customDialects.TryGetValue(connectionTypeName, out ISqlDialect? custom))
        {
            // AUD-R26: custom registrations used to return here directly, bypassing the bulk-copy
            // enhancement step that every built-in resolution goes through, so UseNativeBulkCopy()
            // silently did nothing for them. TryEnhanceWithBulkCopy passes anything that is not one
            // of the four built-in dialects straight back, so routing custom dialects through it
            // changes nothing for them and removes the inconsistency.
            dialect = TryEnhanceWithBulkCopy(custom);
            return true;
        }

        ISqlDialect? resolved = connectionTypeName switch
        {
            "SqlConnection" => new SqlServerDialect(),
            "NpgsqlConnection" => new PostgreSqlDialect(),
            "MySqlConnection" => new MySqlDialect(),
            "SQLiteConnection" or "SqliteConnection" => new SQLiteDialect(),
            _ => null
        };

        if (resolved is null)
        {
            dialect = null;
            return false;
        }

        // Try to enhance dialect with bulk copy support via Extensions.Reflection
        dialect = TryEnhanceWithBulkCopy(resolved);
        return true;
    }

    /// <summary>
    /// Looks through a connection decorator for the connection it wraps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26. Resolution is by <c>connection.GetType().Name</c> alone, which makes the most
    /// likely casualty of the old silent SQL Server fallback a <b>wrapped</b> connection rather
    /// than an exotic engine: MiniProfiler's <c>ProfiledDbConnection</c>, OpenTelemetry and
    /// diagnostic decorators and DI-generated proxies all present a type name matching nothing in
    /// the switch, so a decorated <c>SqliteConnection</c> quietly produced SQL Server SQL. Jaunty
    /// already recognised this shape on the other side of the problem -
    /// <see cref="IDialectWrapper"/> and <see cref="Unwrap"/> see through <em>dialect</em>
    /// decorators - and nothing saw through connection ones.
    /// </para>
    /// <para>
    /// Decorators do not share a base type or interface, so this probes the property names they
    /// conventionally use. If trimming removes the property or the shape is unfamiliar, this
    /// returns false and the caller throws a message naming the type - an error the caller can act
    /// on, which is the point. It never degrades back to guessing a dialect.
    /// </para>
    /// </remarks>
    private static bool TryGetInnerConnection(IDbConnection connection, out IDbConnection? inner)
    {
        Type connectionType = connection.GetType();

        // Spec 011: not a `GetOrAdd(type, BuildInnerConnectionAccessor)` method group. Passing a
        // method whose parameter carried [DynamicallyAccessedMembers] as a delegate produced IL2111,
        // and the trimmer was right - it cannot see through a delegate. The annotation is gone too
        // (it could never be satisfied from a GetType()), leaving the honest suppression on the
        // reflection itself.
        if (!_innerConnectionAccessors.TryGetValue(connectionType, out Func<IDbConnection, IDbConnection?>? accessor))
            accessor = _innerConnectionAccessors.GetOrAdd(connectionType, BuildInnerConnectionAccessor(connectionType));

        inner = accessor?.Invoke(connection);

        // A decorator that returns itself, or null, is not a route to anything.
        if (inner is null || ReferenceEquals(inner, connection))
        {
            inner = null;
            return false;
        }

        return true;
    }

    private static readonly ConcurrentDictionary<Type, Func<IDbConnection, IDbConnection?>?> _innerConnectionAccessors = new();

    /// <summary>Property names connection decorators conventionally expose their inner connection under.</summary>
    private static readonly string[] InnerConnectionPropertyNames = new[]
    {
        "WrappedConnection",    // MiniProfiler ProfiledDbConnection
        "InnerConnection",
        "UnderlyingConnection",
        "BaseConnection",
        "Inner",
    };

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2070",
        Justification = "Optional decorator probe. When trimming removes the member this returns null and GetDialect throws a message naming the type, rather than falling back to a guessed dialect.")]
#endif
    private static Func<IDbConnection, IDbConnection?>? BuildInnerConnectionAccessor(Type connectionType)
    {
        // First the conventional public property, by name. This is the shape real decorators use
        // and the cheapest to be confident about.
        foreach (string name in InnerConnectionPropertyNames)
        {
            PropertyInfo? property = connectionType.GetProperty(
                name, BindingFlags.Public | BindingFlags.Instance);

            if (property is null || property.GetIndexParameters().Length > 0 || !property.CanRead)
                continue;

            if (!typeof(IDbConnection).IsAssignableFrom(property.PropertyType))
                continue;

            return Read(property.GetValue);
        }

        // Then the private field. Plenty of wrappers - including this repo's own test wrapper -
        // hold the connection they decorate in a private field and expose nothing, so a
        // property-only probe would give up on the very shape it exists to handle.
        //
        // Only when there is exactly one candidate. A type holding two connections is not a
        // decorator in any sense this method can resolve, and picking one would be guessing - the
        // behaviour being removed. Zero or two or more, and the caller gets its error instead.
        FieldInfo? single = null;
        // Finding nothing is a supported outcome - the caller gets its own error, which is exactly
        // what a trimmed build that had removed the field would produce. Degrades, never misbehaves.
        // AOT-SAFE: a best-effort probe on a decorator supplied by the caller, not on a Jaunty type.
        foreach (FieldInfo field in connectionType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (!typeof(IDbConnection).IsAssignableFrom(field.FieldType))
                continue;

            if (single is not null)
                return null;

            single = field;
        }

        return single is null ? null : Read(single.GetValue);

        static Func<IDbConnection, IDbConnection?> Read(Func<object?, object?> get) => c =>
        {
            try
            {
                return get(c) as IDbConnection;
            }
            catch
            {
                // A decorator whose accessor throws (disposed, not yet initialised) is simply not a
                // route to an inner connection right now.
                return null;
            }
        };
    }

    /// <summary>
    /// Attempts to enhance dialect with bulk copy support via Jaunty.Extensions.Reflection.
    /// Uses reflection to avoid hard dependency on the extension package.
    /// </summary>
    private static ISqlDialect TryEnhanceWithBulkCopy(ISqlDialect dialect)
    {
        try
        {
            var factoryType = Type.GetType("Jaunty.Extensions.Reflection.Dialects.BulkCopyDialectFactory, Jaunty.Extensions.Reflection");
            if (factoryType == null)
                return dialect;

            // AOT-SAFE: optional-extension probe; when trimming removes the type or method this returns null and the base dialect is used unchanged
            MethodInfo? getDialectMethod = factoryType.GetMethod("GetDialect", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (getDialectMethod == null)
                return dialect;

            var enhanced = getDialectMethod.Invoke(null, new object[] { dialect });
            return enhanced as ISqlDialect ?? dialect;
        }
        catch
        {
            // Extensions.Reflection not loaded or error occurred - use base dialect
            return dialect;
        }
    }
}