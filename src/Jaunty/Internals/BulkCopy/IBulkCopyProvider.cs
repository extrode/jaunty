using System.Data;
using System.Data.Common;

using Jaunty.Configuration;

namespace Jaunty.Internals.BulkCopy;

/// <summary>
/// Provides bulk copy functionality for a specific database provider.
/// Implementations use native bulk copy APIs when available (e.g., SqlBulkCopy, NpgsqlBinaryImporter).
/// </summary>
public interface IBulkCopyProvider
{
    /// <summary>
    /// Gets a value indicating whether this bulk copy provider is supported on the current platform.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Copies data from an <see cref="IDataReader"/> to the specified table.
    /// </summary>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="tableName">The name of the destination table.</param>
    /// <param name="data">The data to copy.</param>
    /// <param name="options">Options for the bulk copy operation.</param>
    /// <returns>The number of rows copied.</returns>
    int CopyToServer(IDbConnection connection, string tableName, IDataReader data, BulkCopyOptions options);

    /// <summary>
    /// Copies data from an <see cref="IDataReader"/> to the specified table asynchronously.
    /// </summary>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="tableName">The name of the destination table.</param>
    /// <param name="data">The data to copy.</param>
    /// <param name="options">Options for the bulk copy operation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The result is the number of rows copied.</returns>
    ValueTask<int> CopyToServerAsync(
        DbConnection connection, 
        string tableName, 
        IDataReader data, 
        BulkCopyOptions options, 
        CancellationToken cancellationToken);
}
