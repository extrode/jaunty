namespace Jaunty.FlatFiles;

/// <summary>
/// Configuration options for creating a flat file database instance.
/// </summary>
public sealed class FlatFileOptions
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
    /// When enabled, verifies that every mapped entity property has a corresponding column in the file.
    /// Extra file columns that are not mapped to entity properties are allowed and silently ignored,
    /// similar to <c>SELECT col1, col2 FROM table</c> semantics.
    /// Default: false.
    /// </summary>
    public bool ValidateSchema { get; set; }

    /// <summary>
    /// Gets or sets whether to preload file data into in-memory tables (CREATE TABLE AS) instead of views.
    /// When true, data is loaded once at registration time, making subsequent queries faster.
    /// Individual sources can override this via their <c>IsPreloaded</c> property.
    /// Default: false.
    /// </summary>
    public bool PreloadIntoMemory { get; set; }

    /// <summary>
    /// Gets the file sources to register on creation.
    /// </summary>
    public List<IFileSource> Sources { get; } = new();

    /// <summary>
    /// Registers a CSV file source mapped to the specified entity type.
    /// The table name is resolved from the entity's <c>[Table]</c> attribute, or the class name lowercased.
    /// </summary>
    public FlatFileOptions AddCsv<T>(string filePath, Action<CsvFileSource>? configure = null) where T : class, new()
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
    public FlatFileOptions AddTsv<T>(string filePath, Action<TsvFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new TsvFileSource(tableName, filePath, typeof(T));
        configure?.Invoke(source);
        Sources.Add(source);
        return this;
    }

    /// <summary>
    /// Registers a Parquet file source mapped to the specified entity type.
    /// The table name is resolved from the entity's <c>[Table]</c> attribute, or the class name lowercased.
    /// </summary>
    public FlatFileOptions AddParquet<T>(string filePath, Action<ParquetFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new ParquetFileSource(tableName, filePath, typeof(T));
        configure?.Invoke(source);
        Sources.Add(source);
        return this;
    }

    /// <summary>
    /// Registers a JSON file source mapped to the specified entity type.
    /// The table name is resolved from the entity's <c>[Table]</c> attribute, or the class name lowercased.
    /// </summary>
    public FlatFileOptions AddJson<T>(string filePath, Action<JsonFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new JsonFileSource(tableName, filePath, typeof(T));
        configure?.Invoke(source);
        Sources.Add(source);
        return this;
    }

    /// <summary>
    /// Adds an Excel (.xlsx) file source for the specified entity type.
    /// Requires the DuckDB <c>excel</c> extension.
    /// </summary>
    /// <typeparam name="T">The entity type to map rows to.</typeparam>
    /// <param name="filePath">The path to the Excel file.</param>
    /// <param name="configure">Optional configuration for the Excel source (sheet name, range, etc.).</param>
    public FlatFileOptions AddExcel<T>(string filePath, Action<ExcelFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new ExcelFileSource(tableName, filePath, typeof(T));
        configure?.Invoke(source);
        Sources.Add(source);
        return this;
    }

    /// <summary>
    /// Adds a Delta Lake table source for the specified entity type.
    /// Requires the DuckDB <c>delta</c> extension.
    /// </summary>
    /// <typeparam name="T">The entity type to map rows to.</typeparam>
    /// <param name="filePath">The path to the Delta Lake table directory.</param>
    /// <param name="configure">Optional configuration for the Delta Lake source.</param>
    public FlatFileOptions AddDeltaLake<T>(string filePath, Action<DeltaLakeFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new DeltaLakeFileSource(tableName, filePath, typeof(T));
        configure?.Invoke(source);
        Sources.Add(source);
        return this;
    }

    /// <summary>
    /// Adds an Apache Iceberg table source for the specified entity type.
    /// Requires the DuckDB <c>iceberg</c> extension.
    /// </summary>
    /// <typeparam name="T">The entity type to map rows to.</typeparam>
    /// <param name="filePath">The path to the Iceberg table directory or metadata file.</param>
    /// <param name="configure">Optional configuration for the Iceberg source.</param>
    public FlatFileOptions AddIceberg<T>(string filePath, Action<IcebergFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new IcebergFileSource(tableName, filePath, typeof(T));
        configure?.Invoke(source);
        Sources.Add(source);
        return this;
    }

    /// <summary>
    /// Registers a custom file source directly. Use this to add file sources
    /// for formats not natively supported (e.g. custom <see cref="IFileSource"/> implementations).
    /// </summary>
    /// <param name="source">The file source to register.</param>
    public FlatFileOptions AddSource(IFileSource source)
    {
        Sources.Add(source ?? throw new ArgumentNullException(nameof(source)));
        return this;
    }

    /// <summary>
    /// Adds a custom file source for the specified entity type. Use this for custom <see cref="IFileSource"/>
    /// implementations that support formats beyond the built-in CSV, TSV, Parquet, JSON, and Excel.
    /// </summary>
    /// <typeparam name="T">The entity type to map rows to.</typeparam>
    /// <typeparam name="TSource">The file source type implementing <see cref="IFileSource"/>.</typeparam>
    /// <param name="filePath">The path to the data file.</param>
    /// <param name="factory">A factory function that creates the file source given (tableName, filePath, entityType).</param>
    /// <param name="configure">Optional configuration callback for the file source.</param>
    public FlatFileOptions AddSource<T, TSource>(string filePath, Func<string, string, Type, TSource> factory, Action<TSource>? configure = null)
        where T : class, new()
        where TSource : IFileSource
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = factory(tableName, filePath, typeof(T));
        configure?.Invoke(source);
        Sources.Add(source);
        return this;
    }
}
