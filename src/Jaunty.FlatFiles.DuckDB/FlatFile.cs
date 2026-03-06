namespace Jaunty.FlatFiles.DuckDB;

/// <summary>
/// Static factory for opening flat file databases backed by DuckDB.
/// </summary>
public static class FlatFile
{
    private static readonly Dictionary<string, Func<string, string, Type, IFileSource>> _extensionRegistry = new(StringComparer.OrdinalIgnoreCase)
    {
        [".csv"] = (tableName, fullPath, entityType) => new CsvFileSource(tableName, fullPath, entityType),
        [".tsv"] = (tableName, fullPath, entityType) => new TsvFileSource(tableName, fullPath, entityType),
        [".parquet"] = (tableName, fullPath, entityType) => new ParquetFileSource(tableName, fullPath, entityType),
        [".json"] = (tableName, fullPath, entityType) => new JsonFileSource(tableName, fullPath, entityType),
        [".ndjson"] = (tableName, fullPath, entityType) => new JsonFileSource(tableName, fullPath, entityType) { JsonFormat = JsonFileFormat.NewlineDelimited },
    };

    /// <summary>
    /// Registers a custom file extension so that <see cref="Open(string)"/> can handle it.
    /// The factory receives (tableName, fullPath, entityType) and must return an <see cref="IFileSource"/>.
    /// </summary>
    /// <param name="extension">The file extension including the dot (e.g. ".xlsx").</param>
    /// <param name="factory">A factory that creates an <see cref="IFileSource"/> from a table name, file path, and entity type.</param>
    public static void RegisterExtension(string extension, Func<string, string, Type, IFileSource> factory)
    {
        ArgumentNullException.ThrowIfNull(extension);
        ArgumentNullException.ThrowIfNull(factory);
        _extensionRegistry[extension.StartsWith('.') ? extension : "." + extension] = factory;
    }

    /// <summary>
    /// Opens a single flat file as a queryable database. The file format is inferred from the extension,
    /// and the view name is derived from the filename (without extension).
    /// </summary>
    /// <param name="filePath">Path to the flat file (.csv, .tsv, .parquet, .json, .ndjson, or any registered extension).</param>
    /// <returns>A flat file database with the file registered as a queryable view.</returns>
    /// <exception cref="ArgumentException">Thrown when the file extension is not supported.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public static IFlatFile Open(string filePath)
    {
        ArgumentNullException.ThrowIfNull(filePath);

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Flat file not found: {fullPath}", fullPath);

        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        var tableName = Path.GetFileNameWithoutExtension(fullPath).ToLowerInvariant();

        var options = new FlatFileOptions();
        var source = CreateSourceFromExtension(extension, tableName, fullPath, typeof(object));
        options.Sources.Add(source);

        return new DuckDb(options);
    }

    /// <summary>
    /// Opens a flat file database with the specified configuration.
    /// Use the builder to register multiple sources with per-source options.
    /// </summary>
    /// <param name="configure">Action to configure the database options and register file sources.</param>
    /// <returns>A flat file database with all configured sources registered.</returns>
    public static IFlatFile Open(Action<FlatFileOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new FlatFileOptions();
        configure(options);

        return new DuckDb(options);
    }

    internal static IFileSource CreateSourceFromExtension(string extension, string tableName, string fullPath, Type entityType)
    {
        if (_extensionRegistry.TryGetValue(extension, out var factory))
            return factory(tableName, fullPath, entityType);

        var supported = string.Join(", ", _extensionRegistry.Keys.OrderBy(k => k));
        throw new ArgumentException(
            $"Unsupported file extension '{extension}'. Supported extensions: {supported}. " +
            $"Use FlatFileDatabase.RegisterExtension() to add custom file types.",
            nameof(extension));
    }
}
