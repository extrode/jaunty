using System.Data.Common;

using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Internals;

namespace Jaunty.FlatFiles.DuckDB;

/// <summary>
/// Provides static convenience methods for importing flat file data into relational databases.
/// </summary>
public static class FlatFileImporter
{
    /// <summary>
    /// Imports data from a flat file into a target database connection in a single call.
    /// The file format is inferred from the extension. The entity type drives table name and column mapping.
    /// </summary>
    /// <typeparam name="T">The entity type. Must have a <c>[Table]</c> attribute or uses class name lowercased.</typeparam>
    /// <param name="filePath">The path to the source flat file (CSV, TSV, Parquet, or JSON).</param>
    /// <param name="targetConnection">The target ADO.NET connection to import into.</param>
    /// <param name="options">Import options (batch size, conflict strategy, etc.).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The total number of rows imported.</returns>
    public static async ValueTask<long> ImportAsync<T>(string filePath, DbConnection targetConnection, ImportOptions options = default, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(targetConnection);

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Flat file not found: {fullPath}", fullPath);

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        var tableName = TableNameResolver.Resolve<T>();

        // Use the registry to create a source with the correct entity type
        var source = FlatFile.CreateSourceFromExtension(extension, tableName, fullPath, typeof(T));
        using var db = FlatFile.Open(opts => opts.AddSource(source));

        return await db.ImportIntoAsync<T>(targetConnection, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Imports data from a configured flat file database into a target database connection.
    /// Useful when the source requires custom configuration (e.g., delimiter, header options).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="configureSource">An action to configure the flat file database options.</param>
    /// <param name="targetConnection">The target ADO.NET connection to import into.</param>
    /// <param name="options">Import options (batch size, conflict strategy, etc.).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The total number of rows imported.</returns>
    public static async ValueTask<long> ImportAsync<T>(Action<FlatFileOptions> configureSource, DbConnection targetConnection, ImportOptions options = default, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configureSource);
        ArgumentNullException.ThrowIfNull(targetConnection);

        using var db = FlatFile.Open(configureSource);
        return await db.ImportIntoAsync<T>(targetConnection, options, cancellationToken).ConfigureAwait(false);
    }
}