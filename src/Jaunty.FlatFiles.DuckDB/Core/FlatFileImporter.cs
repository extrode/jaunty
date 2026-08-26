using System.Data.Common;

using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;
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
    /// <param name="filePath">The path to the source flat file (CSV, TSV, Parquet, or JSON), a glob
    /// pattern (e.g. <c>data/*.csv</c>), or a remote URL (e.g. <c>s3://bucket/file.parquet</c>) -
    /// the same path language <see cref="FlatFile.Open(string)"/> accepts.</param>
    /// <param name="targetConnection">The target ADO.NET connection to import into.</param>
    /// <param name="options">Import options (batch size, conflict strategy, etc.).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    /// <returns>The total number of rows imported.</returns>
    /// <exception cref="ArgumentException">The extension is not supported, or the URI scheme is not allowed.</exception>
    /// <exception cref="FileNotFoundException">A local file does not exist.</exception>
    public static async ValueTask<long> ImportAsync<T>(string filePath, DbConnection targetConnection, ImportOptions options = default, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(targetConnection);

        // AUD-R35-245: shared with FlatFile.Open(string) rather than repeated, so both path-string
        // entry points over this pipeline accept the same path language - a local path, a glob, or
        // one of the allowed remote schemes. This used to be Path.GetFullPath plus File.Exists, so
        // a glob or a URL failed here with a FileNotFoundException naming a missing file. Only the
        // filename-derived table name is discarded: the importer's target table comes from the
        // entity, not the file.
        (string resolvedPath, string extension, _) = FlatFile.ResolvePath(filePath, nameof(filePath));

        var tableName = TableNameResolver.Resolve<T>();

        // Use the registry to create a source with the correct entity type
        IFileSource source = FlatFile.CreateSourceFromExtension(extension, tableName, resolvedPath, typeof(T));
        IFlatFile db = FlatFile.Open(opts => opts.AddSource(source));
        await using var dbDisposer = db.ConfigureAwait(false);

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

        IFlatFile db = FlatFile.Open(configureSource);
        await using var dbDisposer = db.ConfigureAwait(false);

        return await db.ImportIntoAsync<T>(targetConnection, options, cancellationToken).ConfigureAwait(false);
    }
}