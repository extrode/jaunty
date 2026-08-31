using System.Data;
using System.Data.Common;

namespace Jaunty.Configuration;

/// <summary>
/// Provides bulk copy functionality for a specific database provider.
/// Implementations use native bulk copy APIs when available (e.g., SqlBulkCopy, NpgsqlBinaryImporter).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Failure atomicity is provider-dependent.</strong> When <see cref="BulkCopyOptions.Transaction"/>
/// is supplied, every implementation enlists in it and the caller's transaction decides what a partial
/// failure leaves behind. When it is not supplied, the shipped providers differ:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <strong>MySQL</strong> opens its own transaction for the duration of the copy and rolls it back on
///     failure, so an ambient-transaction-free copy is all-or-nothing.
///   </description></item>
///   <item><description>
///     <strong>PostgreSQL</strong> relies on the binary importer's own semantics: the rows become visible
///     only if <c>Complete()</c> is reached, and disposing without completing discards them - so a failed
///     copy also leaves nothing behind, but by a different mechanism.
///   </description></item>
///   <item><description>
///     <strong>SQL Server</strong> does neither. <c>SqlBulkCopy</c> without an external transaction commits
///     each batch as it goes, so a failure part-way through leaves every previously committed batch in the
///     destination table. Pass <see cref="BulkCopyOptions.Transaction"/> if you need all-or-nothing.
///   </description></item>
/// </list>
/// <para>
/// Third-party implementations are under no obligation to match any of these; treat "no transaction
/// supplied" as "atomicity unspecified" unless the specific provider documents otherwise.
/// </para>
/// </remarks>
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
    /// <param name="schemaName">The schema containing the destination table, or <see langword="null"/> to use the connection's default schema.</param>
    /// <param name="tableName">The name of the destination table.</param>
    /// <param name="data">The data to copy.</param>
    /// <param name="options">Options for the bulk copy operation.</param>
    /// <returns>The number of rows copied.</returns>
    int CopyToServer(IDbConnection connection, string? schemaName, string tableName, IDataReader data, BulkCopyOptions options);

    /// <summary>
    /// Copies data from an <see cref="IDataReader"/> to the specified table asynchronously.
    /// </summary>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="schemaName">The schema containing the destination table, or <see langword="null"/> to use the connection's default schema.</param>
    /// <param name="tableName">The name of the destination table.</param>
    /// <param name="data">The data to copy.</param>
    /// <param name="options">Options for the bulk copy operation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The result is the number of rows copied.</returns>
    ValueTask<int> CopyToServerAsync(
        DbConnection connection,
        string? schemaName,
        string tableName,
        IDataReader data,
        BulkCopyOptions options,
        CancellationToken cancellationToken);
}