using System.Collections.Concurrent;
using System.Data;

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

    public static ISqlDialect GetDialect(IDbConnection connection)
    {
        var connectionType = connection.GetType();
        if (_dialectCache.TryGetValue(connectionType, out ISqlDialect? cached))
            return cached;

        var dialect = ResolveDialect(connectionType.Name);
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

    private static ISqlDialect ResolveDialect(string connectionTypeName)
    {
        if (_customDialects.TryGetValue(connectionTypeName, out var custom))
            return custom;

        ISqlDialect dialect = connectionTypeName switch
        {
            "SqlConnection" or "Microsoft.Data.SqlClient.SqlConnection" => new SqlServerDialect(),
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

            var getDialectMethod = factoryType.GetMethod("GetDialect", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
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
