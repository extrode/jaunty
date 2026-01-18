namespace Jaunty.Scaffolding.Configuration;

/// <summary>
/// Supported database providers.
/// </summary>
public enum DatabaseProvider
{
    /// <summary>
    /// Automatically detect the database provider from the connection string.
    /// </summary>
    AutoDetect,

    /// <summary>
    /// Microsoft SQL Server.
    /// </summary>
    SqlServer,

    /// <summary>
    /// PostgreSQL.
    /// </summary>
    PostgreSql,

    /// <summary>
    /// MySQL / MariaDB.
    /// </summary>
    MySql,

    /// <summary>
    /// SQLite.
    /// </summary>
    SQLite
}
