using Jaunty.FlatFiles;

namespace Jaunty.FlatFiles.DuckDB;

/// <summary>
/// Static factory for opening flat file databases backed by DuckDB.
/// </summary>
public static class FlatFileDatabase
{
    /// <summary>
    /// Opens a single flat file as a queryable database. The file format is inferred from the extension,
    /// and the view name is derived from the filename (without extension).
    /// </summary>
    /// <param name="filePath">Path to the flat file (.csv, .tsv, .parquet, .json, .ndjson).</param>
    /// <returns>A flat file database with the file registered as a queryable view.</returns>
    /// <exception cref="ArgumentException">Thrown when the file extension is not supported.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static IFlatFileDatabase Open(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Flat file not found: {fullPath}", fullPath);

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        var tableName = Path.GetFileNameWithoutExtension(fullPath).ToLowerInvariant();

        var options = new FlatFileDatabaseOptions();
        var source = CreateSourceFromExtension(extension, tableName, fullPath);
        options.Sources.Add(source);

        return new DuckDbFlatFileDatabase(options);
    }

    /// <summary>
    /// Opens a flat file database with the specified configuration.
    /// Use the builder to register multiple sources with per-source options.
    /// </summary>
    /// <param name="configure">Action to configure the database options and register file sources.</param>
    /// <returns>A flat file database with all configured sources registered.</returns>
    public static IFlatFileDatabase Open(Action<FlatFileDatabaseOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new FlatFileDatabaseOptions();
        configure(options);

        return new DuckDbFlatFileDatabase(options);
    }

    private static IFileSource CreateSourceFromExtension(string extension, string tableName, string fullPath)
    {
        return extension switch
        {
            ".csv" => new CsvFileSource(tableName, fullPath, typeof(object)),
            ".tsv" => new TsvFileSource(tableName, fullPath, typeof(object)),
            ".parquet" => new ParquetFileSource(tableName, fullPath, typeof(object)),
            ".json" => new JsonFileSource(tableName, fullPath, typeof(object)),
            ".ndjson" => new JsonFileSource(tableName, fullPath, typeof(object)) { JsonFormat = JsonFileFormat.NewlineDelimited },
            _ => throw new ArgumentException(
                $"Unsupported file extension '{extension}'. Supported extensions: .csv, .tsv, .parquet, .json, .ndjson",
                nameof(extension))
        };
    }
}
