using System.Data.Common;

namespace Jaunty.FlatFiles.DuckDB.Internals.Import;

/// <summary>
/// Resolves an <see cref="IImportDialect"/> from a <see cref="DbConnection"/> type.
/// Supports auto-detection for SQLite, PostgreSQL, and SQL Server.
/// Custom dialects can be registered via <see cref="Register"/>.
/// </summary>
public static class ImportDialectResolver
{
    private static readonly Dictionary<string, IImportDialect> _registry = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a custom import dialect for a connection type name (or substring).
    /// The key is matched against <see cref="DbConnection.GetType().FullName"/> using contains logic.
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
        foreach (var (key, dialect) in _registry)
        {
            if (typeName.Contains(key, StringComparison.OrdinalIgnoreCase))
                return dialect;
        }

        // Built-in detection
        if (typeName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            return SqliteImportDialect.Instance;
        if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            return PostgreSqlImportDialect.Instance;
        if (typeName.Contains("SqlConnection", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("Microsoft.Data.SqlClient", StringComparison.OrdinalIgnoreCase))
            return SqlServerImportDialect.Instance;

        // Default fallback
        return SqliteImportDialect.Instance;
    }
}
