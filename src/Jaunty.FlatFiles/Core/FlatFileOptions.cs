using Jaunty.FlatFiles.FileSources;
using Jaunty.FlatFiles.Interfaces;
using Jaunty.FlatFiles.Internals;

namespace Jaunty.FlatFiles.Core;

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
    /// <remarks>
    /// Setting this to <see langword="false"/> while <see cref="Sources"/> is non-empty is not a
    /// supported combination: registering a source runs a command against the connection, which
    /// throws <see cref="InvalidOperationException"/> ("... requires an open connection") if the
    /// connection was never opened.
    /// </remarks>
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
    /// Adds <paramref name="source"/>, rejecting a table name already taken by another source.
    /// </summary>
    /// <remarks>
    /// AUD-R26: every <c>AddXxx&lt;T&gt;</c> resolves its table name from the entity type, so
    /// registering two sources for one entity gave both the same name - and nothing objected at any
    /// layer. <c>Sources</c> is a plain list, <c>DuckDbDialect.GenerateCreateViewSql</c> emits
    /// <c>CREATE OR REPLACE VIEW</c>, and <c>DuckDb._sources</c> is a last-wins dictionary keyed on
    /// entity type. Measured: <c>AddCsv&lt;Sales&gt;(a)</c> then <c>AddCsv&lt;Sales&gt;(b)</c>
    /// constructed successfully and returned only b's rows - a's were silently gone.
    ///
    /// <para>
    /// "Load two files into one entity" is a natural thing to write and the library does support it,
    /// through the multi-path constructor - so the error names it rather than just refusing.
    /// <c>CREATE OR REPLACE</c> stays as it is: re-registering a source deliberately through the
    /// public <c>RegisterSource</c> is a supported operation, and this check is what separates that
    /// from an accidental collision at configuration time.
    /// </para>
    /// </remarks>
    /// <param name="source">The source to add.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when another source already uses the same table name.
    /// </exception>
    private void AddUnique(IFileSource source)
    {
        ThrowIfTableNameTaken(Sources, Sources.Count, source);
        Sources.Add(source);
    }

    /// <summary>
    /// Throws when any two sources in <see cref="Sources"/> share a table name.
    /// </summary>
    /// <remarks>
    /// AUD-R35-072. <see cref="AddUnique"/> is the guard AUD-R26-009 added against two sources
    /// silently sharing a table name, but <see cref="Sources"/> is a public mutable list, so
    /// <c>options.Sources.Add(duplicate)</c> reached <c>DuckDb</c>'s last-wins <c>_sources</c>
    /// dictionary and <c>CREATE OR REPLACE VIEW</c> with the guard never consulted - the exact
    /// silent-data-loss path AUD-R26-009 documents as measured. The library's own
    /// <c>FlatFile.Open(string)</c> takes that route, which is harmless there (fresh options, one
    /// source) but shows the bypass is the natural way to write it. <c>DuckDb</c>'s constructor
    /// calls this before registering, so the guard no longer depends on which API the caller used.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when two sources share a table name.</exception>
    public void EnsureSourceTableNamesAreUnique()
    {
        for (int i = 1; i < Sources.Count; i++)
            ThrowIfTableNameTaken(Sources, i, Sources[i]);
    }

    private static void ThrowIfTableNameTaken(List<IFileSource> sources, int count, IFileSource source)
    {
        for (int i = 0; i < count; i++)
        {
            if (!string.Equals(sources[i].TableName, source.TableName, StringComparison.OrdinalIgnoreCase))
                continue;

            throw new InvalidOperationException(
                $"A file source for table '{source.TableName}' is already registered " +
                $"(entity '{sources[i].EntityType.Name}', path '{sources[i].FilePath}'). " +
                "Two sources cannot share a table name - the second would silently replace the " +
                "first. To read several files as one table, pass them to the source's multi-path " +
                "constructor instead, e.g. new CsvFileSource(tableName, new[] { pathA, pathB }, " +
                "typeof(TEntity)). To map a second file to a different table, give its entity a " +
                "distinct [Table(\"...\")] name.");
        }
    }

    /// <summary>
    /// Registers a CSV file source mapped to the specified entity type.
    /// The table name is resolved from the entity's <c>[Table]</c> attribute, or the class name lowercased.
    /// </summary>
    public FlatFileOptions AddCsv<T>(string filePath, Action<CsvFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new CsvFileSource(tableName, filePath, typeof(T));
        configure?.Invoke(source);
        AddUnique(source);
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
        AddUnique(source);
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
        AddUnique(source);
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
        AddUnique(source);
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
        AddUnique(source);
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
        AddUnique(source);
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
        AddUnique(source);
        return this;
    }

    /// <summary>
    /// Registers a custom file source directly. Use this to add file sources
    /// for formats not natively supported (e.g. custom <see cref="IFileSource"/> implementations).
    /// </summary>
    /// <param name="source">The file source to register.</param>
    public FlatFileOptions AddSource(IFileSource source)
    {
        AddUnique(source ?? throw new ArgumentNullException(nameof(source)));
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
        TSource source = factory(tableName, filePath, typeof(T));
        configure?.Invoke(source);
        AddUnique(source);
        return this;
    }

    /// <summary>
    /// Registers a CSV file source from multiple files mapped to the specified entity type.
    /// </summary>
    public FlatFileOptions AddCsv<T>(string[] filePaths, Action<CsvFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new CsvFileSource(tableName, filePaths, typeof(T));
        configure?.Invoke(source);
        AddUnique(source);
        return this;
    }

    /// <summary>
    /// Registers a TSV file source from multiple files mapped to the specified entity type.
    /// </summary>
    public FlatFileOptions AddTsv<T>(string[] filePaths, Action<TsvFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new TsvFileSource(tableName, filePaths, typeof(T));
        configure?.Invoke(source);
        AddUnique(source);
        return this;
    }

    /// <summary>
    /// Registers a Parquet file source from multiple files mapped to the specified entity type.
    /// </summary>
    public FlatFileOptions AddParquet<T>(string[] filePaths, Action<ParquetFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new ParquetFileSource(tableName, filePaths, typeof(T));
        configure?.Invoke(source);
        AddUnique(source);
        return this;
    }

    /// <summary>
    /// Registers a JSON file source from multiple files mapped to the specified entity type.
    /// </summary>
    public FlatFileOptions AddJson<T>(string[] filePaths, Action<JsonFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new JsonFileSource(tableName, filePaths, typeof(T));
        configure?.Invoke(source);
        AddUnique(source);
        return this;
    }

    /// <summary>
    /// Registers an Excel file source from multiple files mapped to the specified entity type.
    /// </summary>
    public FlatFileOptions AddExcel<T>(string[] filePaths, Action<ExcelFileSource>? configure = null) where T : class, new()
    {
        var tableName = TableNameResolver.Resolve<T>();
        var source = new ExcelFileSource(tableName, filePaths, typeof(T));
        configure?.Invoke(source);
        AddUnique(source);
        return this;
    }
}