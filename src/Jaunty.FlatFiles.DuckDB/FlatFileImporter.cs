using System.Data.Common;
using Jaunty.FlatFiles;

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
    /// <param name="configure">An optional action to configure import options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The total number of rows imported.</returns>
    public static async ValueTask<long> ImportAsync<T>(
        string filePath,
        DbConnection targetConnection,
        Action<ImportOptions>? configure = null,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(targetConnection);

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Flat file not found: {fullPath}", fullPath);

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();

        // Use the configured Open to register with the correct entity type
        using var db = FlatFileDatabase.Open(opts =>
        {
            switch (extension)
            {
                case ".csv": opts.AddCsv<T>(fullPath); break;
                case ".tsv": opts.AddTsv<T>(fullPath); break;
                case ".parquet": opts.AddParquet<T>(fullPath); break;
                case ".json": case ".ndjson": opts.AddJson<T>(fullPath); break;
                default:
                    throw new ArgumentException(
                        $"Unsupported file extension '{extension}'. Supported: .csv, .tsv, .parquet, .json, .ndjson",
                        nameof(filePath));
            }
        });

        return await db.ImportIntoAsync<T>(targetConnection, configure, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Imports data from a configured flat file database into a target database connection.
    /// Useful when the source requires custom configuration (e.g., delimiter, header options).
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="configureSource">An action to configure the flat file database options.</param>
    /// <param name="targetConnection">The target ADO.NET connection to import into.</param>
    /// <param name="configureImport">An optional action to configure import options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The total number of rows imported.</returns>
    public static async ValueTask<long> ImportAsync<T>(
        Action<FlatFileDatabaseOptions> configureSource,
        DbConnection targetConnection,
        Action<ImportOptions>? configureImport = null,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(configureSource);
        ArgumentNullException.ThrowIfNull(targetConnection);

        using var db = FlatFileDatabase.Open(configureSource);
        return await db.ImportIntoAsync<T>(targetConnection, configureImport, cancellationToken).ConfigureAwait(false);
    }
}
