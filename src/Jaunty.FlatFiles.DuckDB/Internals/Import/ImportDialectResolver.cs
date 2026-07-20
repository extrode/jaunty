using System.Collections.Concurrent;
using System.Data.Common;

using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB.Internals.Import;

/// <summary>
/// Resolves an <see cref="IImportDialect"/> from a <see cref="DbConnection"/> type.
/// Supports auto-detection for SQLite, PostgreSQL, and SQL Server.
/// Custom dialects can be registered via <see cref="Register"/>.
/// </summary>
internal static class ImportDialectResolver
{
    private static readonly ConcurrentDictionary<string, IImportDialect> _registry = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a custom import dialect for a connection type name (or substring).
    /// The key is matched against the connection's <c>GetType().FullName</c> using contains logic.
    /// </summary>
    /// <param name="connectionTypeNameContains">A substring of the connection's full type name (e.g. "MySql", "Oracle").</param>
    /// <param name="dialect">The import dialect to use for matching connections.</param>
    public static void Register(string connectionTypeNameContains, IImportDialect dialect)
    {
        ArgumentNullException.ThrowIfNull(connectionTypeNameContains);
        ArgumentNullException.ThrowIfNull(dialect);
        _registry[connectionTypeNameContains] = dialect;
    }

    /// <summary>
    /// Resolves the import dialect for the given connection.
    /// Checks the explicit option first, then custom registrations, then built-in detection.
    /// Falls back to <see cref="SqliteImportDialect"/> for unknown connection types.
    /// </summary>
    internal static IImportDialect Resolve(DbConnection connection, IImportDialect? explicitDialect)
    {
        if (explicitDialect is not null)
            return explicitDialect;

        var typeName = connection.GetType().FullName ?? "";

        // Check custom registrations first
        foreach ((string? key, IImportDialect? dialect) in _registry)
        {
            if (typeName.Contains(key, StringComparison.OrdinalIgnoreCase))
                return dialect;
        }

        // Built-in detection
        if (typeName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return SqliteImportDialect.Instance;
        if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            return PostgreSqlImportDialect.Instance;
        // Match the SqlClient "SqlConnection" type precisely (preceded by a namespace dot) so
        // "MySql.Data.MySqlClient.MySqlConnection" doesn't false-positive on the "SqlConnection" substring.
        if (typeName.EndsWith(".SqlConnection", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("Microsoft.Data.SqlClient", StringComparison.OrdinalIgnoreCase))
            return SqlServerImportDialect.Instance;

        // Default fallback
        return SqliteImportDialect.Instance;
    }
}