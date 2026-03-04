namespace Jaunty.FlatFiles;

/// <summary>
/// Configuration options for creating a flat file database instance.
/// </summary>
public sealed class FlatFileDatabaseOptions
{
    /// <summary>
    /// Gets or sets the path to the database file. Use ":memory:" for in-memory databases.
    /// Default: ":memory:".
    /// </summary>
    public string DatabasePath { get; set; } = ":memory:";

    /// <summary>
    /// Gets or sets whether to open the connection immediately on creation.
    /// Default: true.
    /// </summary>
    public bool AutoOpen { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to register the dialect with <c>SqlDialectFactory</c>.
    /// Default: true.
    /// </summary>
    public bool RegisterDialect { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to validate entity schema against inferred file schema on registration.
    /// Default: false.
    /// </summary>
    public bool ValidateSchema { get; set; }

    /// <summary>
    /// Gets the file sources to register on creation.
    /// </summary>
    public List<IFileSource> Sources { get; } = new();

    /// <summary>
    /// Registers a CSV file source mapped to the specified entity type.
    /// The table name is resolved from the entity's <c>[Table]</c> attribute, or the class name lowercased.
    /// </summary>
    public FlatFileDatabaseOptions AddCsv<T>(string filePath, Action<CsvFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new CsvFileSource(tableName, filePath, typeof(T));
        configure?.Invoke(source);
        Sources.Add(source);
        return this;
    }

    /// <summary>
    /// Registers a TSV file source mapped to the specified entity type.
    /// The table name is resolved from the entity's <c>[Table]</c> attribute, or the class name lowercased.
    /// </summary>
    public FlatFileDatabaseOptions AddTsv<T>(string filePath, Action<TsvFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new TsvFileSource(tableName, filePath, typeof(T));
        configure?.Invoke(source);
        Sources.Add(source);
        return this;
    }
}
