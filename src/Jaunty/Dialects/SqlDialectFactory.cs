using System.Collections.Concurrent;
using System.Data;
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
    /// <remarks>
    /// Dialects are cached per connection type for optimal performance.
    /// Custom dialects registered via <see cref="RegisterDialect(string, ISqlDialect)"/> take priority.
    /// </remarks>
    public static ISqlDialect GetDialect(IDbConnection connection)
    {
        Type connectionType = connection.GetType();
        if (_dialectCache.TryGetValue(connectionType, out ISqlDialect? cached))
            return cached;

        ISqlDialect dialect = ResolveDialect(connectionType.Name);
        _dialectCache.TryAdd(connectionType, dialect);
        return dialect;
    }

    /// <summary>
    /// Registers a custom SQL dialect for a specific connection type name.
    /// Custom registrations take priority over built-in dialect resolution.
    /// </summary>
    /// <param name="connectionTypeName">The Type.Name of the connection class (e.g., "DuckDBConnection").</param>
    /// <param name="dialect">The dialect instance to use for that connection type.</param>
    public static void RegisterDialect(string connectionTypeName, ISqlDialect dialect)
    {
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
    /// Registers a custom SQL dialect for a specific connection type.
    /// Custom registrations take priority over built-in dialect resolution.
    /// </summary>
    /// <typeparam name="TConnection">The connection type to register the dialect for.</typeparam>
    /// <param name="dialect">The dialect instance to use for that connection type.</param>
    public static void RegisterDialect<TConnection>(ISqlDialect dialect) where TConnection : IDbConnection
    {
        var typeName = typeof(TConnection).Name;
        _customDialects[typeName] = dialect;
        _dialectCache[typeof(TConnection)] = dialect;
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

    private static ISqlDialect ResolveDialect(string connectionTypeName)
    {
        if (_customDialects.TryGetValue(connectionTypeName, out ISqlDialect? custom))
            return custom;

        ISqlDialect dialect = connectionTypeName switch
        {
            "SqlConnection" => new SqlServerDialect(),
            "NpgsqlConnection" => new PostgreSqlDialect(),
            "MySqlConnection" => new MySqlDialect(),
            "SQLiteConnection" or "SqliteConnection" => new SQLiteDialect(),
            _ => new SqlServerDialect() // Default to SQL Server
        };

        // Try to enhance dialect with bulk copy support via Extensions.Reflection
        return TryEnhanceWithBulkCopy(dialect);
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