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
        return connectionTypeName switch
        {
            "SqlConnection" or "Microsoft.Data.SqlClient.SqlConnection" => new SqlServerDialect(),
            "NpgsqlConnection" => new PostgreSqlDialect(),
            "MySqlConnection" => new MySqlDialect(),
            "SQLiteConnection" or "SqliteConnection" => new SQLiteDialect(),
            _ => new SqlServerDialect() // Default to SQL Server
        };
    }
}
