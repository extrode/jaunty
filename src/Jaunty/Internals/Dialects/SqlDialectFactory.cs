using System.Data;

namespace Jaunty.Internals.Dialects;

/// <summary>
/// Factory for creating SQL dialects based on connection type.
/// Auto-detects the database provider from the connection object.
/// </summary>
internal static class SqlDialectFactory
{
    public static ISqlDialect GetDialect(IDbConnection connection)
    {
        var connectionTypeName = connection.GetType().Name;

        return connectionTypeName switch
        {
            "SqlConnection" or "Microsoft.Data.SqlClient.SqlConnection" => new SqlServerDialect(),
            "NpgsqlConnection" => new PostgreSqlDialect(),
            "MySqlConnection" => new MySqlDialect(),
            "SQLiteConnection" => new SQLiteDialect(),
            _ => new SqlServerDialect() // Default to SQL Server
        };
    }
}
