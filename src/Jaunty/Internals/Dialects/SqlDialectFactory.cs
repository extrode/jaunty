using System.Collections.Concurrent;
using System.Data;

namespace Jaunty.Internals.Dialects;

/// <summary>
/// Factory for creating SQL dialects based on connection type.
/// Auto-detects the database provider from the connection object.
/// Dialect instances are cached per connection type for zero-allocation lookups.
/// </summary>
internal static class SqlDialectFactory
{
    private static readonly ConcurrentDictionary<Type, ISqlDialect> _dialectCache = new();

    public static ISqlDialect GetDialect(IDbConnection connection)
    {
        var connectionType = connection.GetType();
        if (_dialectCache.TryGetValue(connectionType, out ISqlDialect? cached))
            return cached;

        var dialect = ResolveDialect(connectionType.Name);
        _dialectCache.TryAdd(connectionType, dialect);
        return dialect;
    }

    private static ISqlDialect ResolveDialect(string connectionTypeName)
    {
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
