using Jaunty.Scaffolding.Schema;

namespace Jaunty.Scaffolding.Abstractions;

/// <summary>
/// Interface for reading database schema information.
/// </summary>
public interface ISchemaReader
{
    /// <summary>
    /// Reads the database schema from the connection.
    /// </summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="options">Options for filtering tables and schemas.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The database schema containing all requested tables.</returns>
    Task<DatabaseSchema> ReadSchemaAsync(
        string connectionString,
        SchemaReaderOptions options,
        CancellationToken cancellationToken = default);
}
