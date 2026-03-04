using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using DuckDB.NET.Data;
using Jaunty.Attributes;
using Jaunty.Dialects;
using Jaunty.FlatFiles;

namespace Jaunty.FlatFiles.DuckDB;

/// <summary>
/// A flat file database backed by DuckDB. Creates an in-memory (or file-backed) DuckDB instance
/// and registers flat file sources as queryable views.
/// </summary>
public sealed class DuckDbFlatFileDatabase : IFlatFileDatabase
{
    private readonly DuckDBConnection _connection;
    private readonly DuckDbDialect _dialect;
    private readonly FlatFileDatabaseOptions _options;
    private readonly ConcurrentDictionary<Type, IFileSource> _sources = new();
    private readonly ConcurrentDictionary<Type, bool> _modified = new();
    private bool _disposed;

    /// <inheritdoc />
    public IDbConnection Connection => _connection;

    /// <summary>
    /// Creates a new DuckDB flat file database with default options (in-memory).
    /// </summary>
    public DuckDbFlatFileDatabase()
        : this(new FlatFileDatabaseOptions())
    {
    }

    /// <summary>
    /// Creates a new DuckDB flat file database with the specified options.
    /// </summary>
    /// <param name="options">Configuration options for the database.</param>
    public DuckDbFlatFileDatabase(FlatFileDatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _dialect = DuckDbDialect.Instance;

        var connectionString = options.DatabasePath == ":memory:"
            ? "DataSource=:memory:"
            : $"DataSource={options.DatabasePath}";

        _connection = new DuckDBConnection(connectionString);

        if (options.RegisterDialect)
        {
            SqlDialectFactory.RegisterDialect("DuckDBConnection", _dialect);
        }

        if (options.AutoOpen)
        {
            _connection.Open();
        }

        foreach (var source in options.Sources)
        {
            // Apply global PreloadIntoMemory if the source hasn't been explicitly configured
            if (options.PreloadIntoMemory && !source.IsPreloaded)
            {
                ApplyPreload(source);
            }
            RegisterSource(source);
        }
    }

    /// <inheritdoc />
    public void RegisterSource(IFileSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sql = GenerateRegistrationSql(source);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();

        if (_options.ValidateSchema && source.EntityType != typeof(object))
        {
            ValidateSchema(source);
        }

        _sources[source.EntityType] = source;
    }

    /// <inheritdoc />
    public async ValueTask RegisterSourceAsync(IFileSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sql = GenerateRegistrationSql(source);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        if (_options.ValidateSchema && source.EntityType != typeof(object))
        {
            ValidateSchema(source);
        }

        _sources[source.EntityType] = source;
    }

    /// <inheritdoc />
    public IFileSource? GetSource<T>() where T : class, new()
    {
        return _sources.TryGetValue(typeof(T), out var source) ? source : null;
    }

    /// <inheritdoc />
    public bool IsModified<T>() where T : class, new()
    {
        return _modified.TryGetValue(typeof(T), out var modified) && modified;
    }

    /// <summary>
    /// Gets the DuckDB dialect instance used by this database.
    /// </summary>
    public DuckDbDialect Dialect => _dialect;

    // ==========================================
    // CRUD Operations
    // ==========================================

    /// <inheritdoc />
    public async ValueTask<int> InsertAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(entity);

        var source = GetSourceOrThrow<T>();
        await EnsurePromotedToTableAsync(source, cancellationToken).ConfigureAwait(false);

        var mappings = FlatFileExpressionHelper.GetColumnMappings(typeof(T));
        var columns = new StringBuilder();
        var values = new StringBuilder();
        var parameters = new List<DuckDBParameter>();

        for (int i = 0; i < mappings.Count; i++)
        {
            if (i > 0) { columns.Append(", "); values.Append(", "); }
            var (columnName, prop) = mappings[i];
            columns.Append($"\"{columnName}\"");
            values.Append($"${i + 1}"); // DuckDB uses 1-based positional params
            parameters.Add(new DuckDBParameter { Value = prop.GetValue(entity) ?? DBNull.Value });
        }

        var sql = $"INSERT INTO \"{source.TableName}\" ({columns}) VALUES ({values})";
        var result = await ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
        _modified[typeof(T)] = true;
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<int> InsertAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(entities);

        var entityList = entities as IList<T> ?? entities.ToList();
        if (entityList.Count == 0) return 0;

        var source = GetSourceOrThrow<T>();
        await EnsurePromotedToTableAsync(source, cancellationToken).ConfigureAwait(false);

        var mappings = FlatFileExpressionHelper.GetColumnMappings(typeof(T));
        var columns = new StringBuilder();
        for (int i = 0; i < mappings.Count; i++)
        {
            if (i > 0) columns.Append(", ");
            columns.Append($"\"{mappings[i].ColumnName}\"");
        }

        var totalInserted = 0;

        // Batch insert using multi-row VALUES with positional parameters ($1, $2, ...)
        var sb = new StringBuilder();
        var parameters = new List<DuckDBParameter>();
        var paramCounter = 0;

        for (int row = 0; row < entityList.Count; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (row > 0) sb.Append(", ");
            sb.Append('(');
            var entity = entityList[row];

            for (int col = 0; col < mappings.Count; col++)
            {
                if (col > 0) sb.Append(", ");
                paramCounter++;
                sb.Append($"${paramCounter}");
                parameters.Add(new DuckDBParameter { Value = mappings[col].Property.GetValue(entity) ?? DBNull.Value });
            }
            sb.Append(')');
        }

        var sql = $"INSERT INTO \"{source.TableName}\" ({columns}) VALUES {sb}";
        totalInserted = await ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);

        if (totalInserted > 0) _modified[typeof(T)] = true;
        return totalInserted;
    }

    /// <inheritdoc />
    public async ValueTask<int> UpdateAsync<T>(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, object>> column,
        object value,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(column);

        var source = GetSourceOrThrow<T>();
        await EnsurePromotedToTableAsync(source, cancellationToken).ConfigureAwait(false);

        var columnName = FlatFileExpressionHelper.ResolveColumnName(column);

        // SET parameter is $1, WHERE parameters start at $2
        var setParam = new DuckDBParameter { Value = value ?? DBNull.Value };
        var (whereSql, whereParams) = FlatFileExpressionHelper.TranslatePredicate(predicate, paramOffset: 1);

        // Build final parameter list: SET param first, then WHERE params
        var allParams = new List<DuckDBParameter>(whereParams.Count + 1) { setParam };
        allParams.AddRange(whereParams);

        var sql = $"UPDATE \"{source.TableName}\" SET \"{columnName}\" = $1 WHERE {whereSql}";
        var result = await ExecuteNonQueryAsync(sql, allParams, cancellationToken).ConfigureAwait(false);

        if (result > 0) _modified[typeof(T)] = true;
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<int> DeleteAsync<T>(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var source = GetSourceOrThrow<T>();
        await EnsurePromotedToTableAsync(source, cancellationToken).ConfigureAwait(false);

        var (whereSql, whereParams) = FlatFileExpressionHelper.TranslatePredicate(predicate);

        var sql = $"DELETE FROM \"{source.TableName}\" WHERE {whereSql}";
        var result = await ExecuteNonQueryAsync(sql, whereParams, cancellationToken).ConfigureAwait(false);

        if (result > 0) _modified[typeof(T)] = true;
        return result;
    }

    // ==========================================
    // Write-Back Operations
    // ==========================================

    /// <inheritdoc />
    public async ValueTask SaveAsync<T>(string outputPath, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        var source = GetSourceOrThrow<T>();
        var format = InferFormatFromExtension(outputPath);
        var sql = _dialect.GenerateCopyToSql(source.TableName, Path.GetFullPath(outputPath), format);

        await ExecuteNonQueryAsync(sql, [], cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync<T>(WriteBackMode mode, CancellationToken cancellationToken = default) where T : class, new()
    {
        var source = GetSourceOrThrow<T>();

        if (mode == WriteBackMode.NewFile)
        {
            throw new InvalidOperationException(
                "WriteBackMode.NewFile requires an output path. Use the SaveAsync<T>(string outputPath) overload instead.");
        }

        // WriteBackMode.Overwrite — atomic replace: write to temp file, then rename
        var originalPath = source.FilePath;
        var directory = Path.GetDirectoryName(originalPath) ?? ".";
        var tempPath = Path.Combine(directory, $".{Path.GetFileNameWithoutExtension(originalPath)}.tmp{Path.GetExtension(originalPath)}");

        try
        {
            var sql = _dialect.GenerateCopyToSql(source.TableName, Path.GetFullPath(tempPath), source.Format);
            await ExecuteNonQueryAsync(sql, [], cancellationToken).ConfigureAwait(false);

            // Atomic replace: delete original, rename temp
            File.Delete(originalPath);
            File.Move(tempPath, originalPath);
        }
        catch
        {
            // Clean up temp file on failure
            try { File.Delete(tempPath); } catch { }
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask ExportAsync<T>(string outputPath, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        var source = GetSourceOrThrow<T>();
        var format = InferFormatFromExtension(outputPath);
        var sql = _dialect.GenerateCopyToSql(source.TableName, Path.GetFullPath(outputPath), format);

        await ExecuteNonQueryAsync(sql, [], cancellationToken).ConfigureAwait(false);
    }

    // ==========================================
    // Private Helpers
    // ==========================================

    private IFileSource GetSourceOrThrow<T>() where T : class, new()
    {
        if (!_sources.TryGetValue(typeof(T), out var source))
        {
            throw new InvalidOperationException(
                $"No file source registered for entity type '{typeof(T).Name}'. " +
                $"Register it via AddCsv<{typeof(T).Name}>(), AddParquet<{typeof(T).Name}>(), etc.");
        }
        return source;
    }

    /// <summary>
    /// Promotes a VIEW source to a TABLE on first mutation. Preloaded sources and already-promoted sources are no-ops.
    /// </summary>
    private async ValueTask EnsurePromotedToTableAsync(IFileSource source, CancellationToken cancellationToken)
    {
        // Already a table (preloaded or previously promoted)
        if (source.IsPreloaded || source.IsPromotedToTable) return;

        var sql = _dialect.GeneratePromoteToTableSql(source);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        // Mark as promoted
        SetPromotedToTable(source);
    }

    private static void SetPromotedToTable(IFileSource source)
    {
        switch (source)
        {
            case CsvFileSource csv: csv.IsPromotedToTable = true; break;
            case TsvFileSource tsv: tsv.IsPromotedToTable = true; break;
            case ParquetFileSource parquet: parquet.IsPromotedToTable = true; break;
            case JsonFileSource json: json.IsPromotedToTable = true; break;
        }
    }

    private async ValueTask<int> ExecuteNonQueryAsync(string sql, List<DuckDBParameter> parameters, CancellationToken cancellationToken)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var param in parameters)
            cmd.Parameters.Add(param);
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static FileFormat InferFormatFromExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".csv" => FileFormat.Csv,
            ".tsv" => FileFormat.Tsv,
            ".parquet" => FileFormat.Parquet,
            ".json" or ".ndjson" => FileFormat.Json,
            _ => throw new ArgumentException(
                $"Cannot infer output format from extension '{ext}'. Supported: .csv, .tsv, .parquet, .json, .ndjson",
                nameof(path))
        };
    }

    /// <summary>
    /// Sets IsPreloaded on a source. Works with all concrete source types.
    /// </summary>
    private static void ApplyPreload(IFileSource source)
    {
        switch (source)
        {
            case CsvFileSource csv: csv.IsPreloaded = true; break;
            case TsvFileSource tsv: tsv.IsPreloaded = true; break;
            case ParquetFileSource parquet: parquet.IsPreloaded = true; break;
            case JsonFileSource json: json.IsPreloaded = true; break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Generates the SQL to register a file source, handling DATE→TIMESTAMP casting
    /// for entity properties of type DateTime.
    /// </summary>
    private string GenerateRegistrationSql(IFileSource source)
    {
        // If we have entity metadata, check for DateTime properties that need DATE→TIMESTAMP casting.
        // DuckDB's read_csv_auto infers DATE for date-only values, but Jaunty's materialization
        // uses Convert.ChangeType which fails on DuckDB's DuckDBDateOnly type.
        // Solution: generate a view with explicit CAST for DateTime columns.
        if (source.EntityType != typeof(object) && HasDateTimeProperties(source.EntityType))
        {
            return GenerateViewSqlWithDateTimeCasts(source);
        }

        return source.IsPreloaded
            ? _dialect.GenerateCreateTableAsSql(source)
            : _dialect.GenerateCreateViewSql(source);
    }

    /// <summary>
    /// Generates a CREATE VIEW with CAST for DateTime properties.
    /// Uses a two-step approach: first queries column names from the read function,
    /// then generates a SELECT with explicit CAST for date columns.
    /// </summary>
    private string GenerateViewSqlWithDateTimeCasts(IFileSource source)
    {
        var readFunction = DuckDbDialect.GenerateReadFunction(source);
        var dateTimeColumns = GetDateTimeColumnNames(source.EntityType);

        // Query the file to get actual column names
        List<string> fileColumns;
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT * FROM {readFunction} LIMIT 0";
            using var reader = cmd.ExecuteReader();
            fileColumns = new List<string>(reader.FieldCount);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                fileColumns.Add(reader.GetName(i));
            }
        }

        // Build SELECT with CASTs
        var sb = new StringBuilder();
        for (int i = 0; i < fileColumns.Count; i++)
        {
            if (i > 0) sb.Append(", ");

            var col = fileColumns[i];
            if (dateTimeColumns.Contains(col))
            {
                sb.Append($"CAST(\"{col}\" AS TIMESTAMP) AS \"{col}\"");
            }
            else
            {
                sb.Append($"\"{col}\"");
            }
        }

        var keyword = source.IsPreloaded ? "TABLE" : "VIEW";
        return $"CREATE OR REPLACE {keyword} \"{source.TableName}\" AS SELECT {sb} FROM {readFunction}";
    }

    /// <summary>
    /// Gets the set of column names (from the file, not C# property names) that map to DateTime properties.
    /// </summary>
    private static HashSet<string> GetDateTimeColumnNames(Type entityType)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (propType != typeof(DateTime)) continue;

            // Use [Column] attribute name if present, otherwise property name
            var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var columnName = columnAttr?.Name ?? prop.Name;
            result.Add(columnName);
        }
        return result;
    }

    private static bool HasDateTimeProperties(Type entityType)
    {
        foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (propType == typeof(DateTime)) return true;
        }
        return false;
    }

    /// <summary>
    /// Validates that the file's inferred schema is compatible with the entity type.
    /// </summary>
    private void ValidateSchema(IFileSource source)
    {
        // Get columns from the view
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"DESCRIBE \"{source.TableName}\"";
        using var reader = cmd.ExecuteReader();

        var fileColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            var colName = reader.GetString(0);
            var colType = reader.GetString(1);
            fileColumns[colName] = colType;
        }

        // Validate entity properties have matching columns
        foreach (var prop in source.EntityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();
            var columnName = columnAttr?.Name ?? prop.Name;

            if (!fileColumns.ContainsKey(columnName))
            {
                throw new InvalidOperationException(
                    $"Schema validation failed for '{source.TableName}': Entity property '{prop.Name}' " +
                    $"maps to column '{columnName}' which does not exist in the file. " +
                    $"Available columns: {string.Join(", ", fileColumns.Keys)}");
            }
        }
    }
}
