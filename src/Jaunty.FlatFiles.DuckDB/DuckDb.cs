using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

using DuckDB.NET.Data;

using Jaunty.Dialects;
using Jaunty.FlatFiles.DuckDB.ImportPipeline;
using Jaunty.Fluent;

namespace Jaunty.FlatFiles.DuckDB;

/// <summary>
/// A flat file database backed by DuckDB. Creates an in-memory (or file-backed) DuckDB instance
/// and registers flat file sources as queryable views.
/// </summary>
public sealed class DuckDb : IFlatFile
{
    private readonly DuckDBConnection _connection;
    private readonly DuckDbDialect _dialect;
    private readonly FlatFileOptions _options;
    private readonly ConcurrentDictionary<Type, IFileSource> _sources = new();
    private readonly ConcurrentDictionary<Type, bool> _modified = new();
    private bool _disposed;

    /// <inheritdoc />
    public IDbConnection Connection => _connection;

    /// <summary>
    /// Creates a new DuckDB flat file database with default options (in-memory).
    /// </summary>
    public DuckDb()
        : this(new FlatFileOptions())
    {
    }

    /// <summary>
    /// Creates a new DuckDB flat file database with the specified options.
    /// </summary>
    /// <param name="options">Configuration options for the database.</param>
    public DuckDb(FlatFileOptions options)
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
    // Query Operations
    // ==========================================

    /// <inheritdoc />
    public IFromClause<T> Query<T>() where T : class, new()
    {
        var source = GetSourceOrThrow<T>();
        // Use Jaunty.Fluent's From which returns IFromClause<T> for fluent queries
        // The table name will be resolved from [Table] attribute on the entity
        return FluentExtensions.From<T>(_connection);
    }

    /// <inheritdoc />
    public async ValueTask<List<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken = default) where T : class, new()
    {
        return await QueryAsync<T>(sql, Enumerable.Empty<(string, object?)>(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask<List<T>> QueryAsync<T>(string sql, IEnumerable<(string Name, object? Value)> parameters,         CancellationToken cancellationToken = default) where T : class, new()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;

        // DuckDB uses positional parameters ($1, $2, ...) - add in order
        foreach (var (_, value) in parameters)
        {
            var param = cmd.CreateParameter();
            param.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(param);
        }

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        // Build a case-insensitive column name → ordinal map once before the row loop.
        // DuckDB lowercases column names, so exact-match lookups via GetOrdinal can fail
        // for PascalCase entity properties. A dictionary avoids per-column O(n) fallback scans.
        var columnOrdinals = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reader.FieldCount; i++)
            columnOrdinals[reader.GetName(i)] = i;

        var mappings = FlatFileExpressionHelper.GetColumnMappings(typeof(T));

        // Pre-resolve ordinals for each mapping (once, not per row)
        var ordinalMap = new int[mappings.Count];
        for (int i = 0; i < mappings.Count; i++)
            ordinalMap[i] = columnOrdinals.TryGetValue(mappings[i].ColumnName, out var ord) ? ord : -1;

        // Materialize rows
        var results = new List<T>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var entity = new T();
            for (int i = 0; i < mappings.Count; i++)
            {
                var ordinal = ordinalMap[i];
                if (ordinal >= 0 && !reader.IsDBNull(ordinal))
                {
                    var value = reader.GetValue(ordinal);
                    // Convert value to property type if needed
                    var targetType = mappings[i].PropertyType;
                    if (value != null && value.GetType() != targetType)
                    {
                        try
                        {
                            value = Convert.ChangeType(value, Nullable.GetUnderlyingType(targetType) ?? targetType);
                        }
                        catch
                        {
                            // If conversion fails, let the property setter handle it
                        }
                    }
                    mappings[i].Setter(entity, value);
                }
            }
            results.Add(entity);
        }
        return results;
    }

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
        // Pre-size StringBuilders to avoid reallocations (~20 chars per column name, ~10 chars per parameter)
        var columns = new StringBuilder(mappings.Count * 20);
        var values = new StringBuilder(mappings.Count * 10);
        var parameters = new List<DuckDBParameter>(mappings.Count);

        for (int i = 0; i < mappings.Count; i++)
        {
            if (i > 0) { columns.Append(", "); values.Append(", "); }
            var mapping = mappings[i];
            columns.Append($"\"{mapping.ColumnName}\"");
            values.Append($"${i + 1}"); // DuckDB uses 1-based positional params
            parameters.Add(new DuckDBParameter { Value = mapping.Getter(entity) ?? DBNull.Value });
        }

        var sql = $"INSERT INTO \"{source.TableName}\" ({columns}) VALUES ({values})";
        var result = await ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
        _modified.TryAdd(typeof(T), true);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<int> InsertAsync<T>(IEnumerable<T> entities, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(entities);

        // Avoid unnecessary ToList() if already a list
        // Check common collection types first to avoid allocation
        var entityList = entities switch
        {
            IList<T> list => list,
            ICollection<T> collection => collection.ToList(), // At least we know the count
            _ => entities.ToList()
        };
        
        if (entityList.Count == 0) return 0;

        var source = GetSourceOrThrow<T>();
        await EnsurePromotedToTableAsync(source, cancellationToken).ConfigureAwait(false);

        var mappings = FlatFileExpressionHelper.GetColumnMappings(typeof(T));
        // Pre-size StringBuilder (~20 chars per column name)
        var columns = new StringBuilder(mappings.Count * 20);
        for (int i = 0; i < mappings.Count; i++)
        {
            if (i > 0) columns.Append(", ");
            columns.Append($"\"{mappings[i].ColumnName}\"");
        }

        var totalInserted = 0;

        // Batch insert using multi-row VALUES with positional parameters ($1, $2, ...)
        // Pre-size StringBuilder: ~10 chars per value * mappings.Count * entityList.Count
        var sb = new StringBuilder(entityList.Count * mappings.Count * 10);
        var parameters = new List<DuckDBParameter>(entityList.Count * mappings.Count);
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
                parameters.Add(new DuckDBParameter { Value = mappings[col].Getter(entity) ?? DBNull.Value });
            }
            sb.Append(')');
        }

        var sql = $"INSERT INTO \"{source.TableName}\" ({columns}) VALUES {sb}";
        totalInserted = await ExecuteNonQueryAsync(sql, parameters, cancellationToken).ConfigureAwait(false);

        if (totalInserted > 0) _modified.TryAdd(typeof(T), true);
        return totalInserted;
    }

    /// <inheritdoc />
    public async ValueTask<int> UpdateAsync<T>(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> column,         object value, CancellationToken cancellationToken = default) where T : class, new()
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

        if (result > 0) _modified.TryAdd(typeof(T), true);
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<int> DeleteAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var source = GetSourceOrThrow<T>();
        await EnsurePromotedToTableAsync(source, cancellationToken).ConfigureAwait(false);

        var (whereSql, whereParams) = FlatFileExpressionHelper.TranslatePredicate(predicate);

        var sql = $"DELETE FROM \"{source.TableName}\" WHERE {whereSql}";
        var result = await ExecuteNonQueryAsync(sql, whereParams, cancellationToken).ConfigureAwait(false);

        if (result > 0) _modified.TryAdd(typeof(T), true);
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
            var sql = _dialect.GenerateCopyToSql(source.TableName, Path.GetFullPath(tempPath), source);
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
    // Import Operations
    // ==========================================

    /// <inheritdoc />
    public async ValueTask<long> ImportIntoAsync<T>(DbConnection targetConnection, ImportOptions options = default, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(targetConnection);

        var source = GetSourceOrThrow<T>();

        return await ImportExecutor.ExecuteAsync<T>(
            _connection, source, targetConnection, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Attaches an external database (MySQL, PostgreSQL, or SQLite) to DuckDB, making its tables queryable.
    /// Requires the corresponding DuckDB extension to be installed and loaded.
    /// </summary>
    /// <param name="connectionString">The connection string for the external database.</param>
    /// <param name="alias">The alias to use when referencing the attached database in queries (e.g. <c>SELECT * FROM alias.table</c>).</param>
    /// <param name="type">The database type: <c>"mysql"</c>, <c>"postgres"</c>, or <c>"sqlite"</c>.</param>
    /// <param name="readOnly">Whether to attach in read-only mode. Default: false.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    public async ValueTask AttachAsync(string connectionString, string alias, string type, bool readOnly = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        ArgumentNullException.ThrowIfNull(alias);
        ArgumentNullException.ThrowIfNull(type);

        var escapedConnStr = connectionString.Replace("'", "''");
        var escapedAlias = alias.Replace("\"", "\"\"");
        var escapedType = type.Replace("'", "''");

        var sql = $"ATTACH '{escapedConnStr}' AS \"{escapedAlias}\" (TYPE {escapedType}{(readOnly ? ", READ_ONLY" : "")})";
        await ExecuteNonQueryAsync(sql, [], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Detaches a previously attached external database.
    /// </summary>
    /// <param name="alias">The alias of the database to detach.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation.</param>
    public async ValueTask DetachAsync(string alias, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alias);

        var escapedAlias = alias.Replace("\"", "\"\"");
        await ExecuteNonQueryAsync($"DETACH \"{escapedAlias}\"", [], cancellationToken).ConfigureAwait(false);
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
    /// Uses a transaction to ensure the three-step promotion (CREATE TABLE AS, DROP VIEW, RENAME) is atomic.
    /// </summary>
    private async ValueTask EnsurePromotedToTableAsync(IFileSource source, CancellationToken cancellationToken)
    {
        // Already a table (preloaded or previously promoted)
        if (source.IsPreloaded || source.IsPromotedToTable) return;

        var sql = _dialect.GeneratePromoteToTableSql(source);

        var transaction = await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = transaction;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        // Mark as promoted
        source.IsPromotedToTable = true;
    }

    private async ValueTask<int> ExecuteNonQueryAsync(string sql, List<DuckDBParameter> parameters, CancellationToken cancellationToken)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var param in parameters)
            cmd.Parameters.Add(param);
        return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private string InferFormatFromExtension(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        // Check built-in formats first
        var format = ext switch
        {
            ".csv" => FileFormats.Csv,
            ".tsv" => FileFormats.Tsv,
            ".parquet" => FileFormats.Parquet,
            ".json" or ".ndjson" => FileFormats.Json,
            ".xlsx" or ".xls" => FileFormats.Excel,
            _ => (string?)null
        };

        if (format is not null)
            return format;

        throw new ArgumentException(
            $"Cannot infer output format from extension '{ext}'. " +
            $"Supported extensions: .csv, .tsv, .parquet, .json, .ndjson, .xlsx, .xls.",
            nameof(path));
    }

    /// <summary>
    /// Sets IsPreloaded on a source via the IFileSource interface setter.
    /// </summary>
    private static void ApplyPreload(IFileSource source)
    {
        source.IsPreloaded = true;
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
        if (source.EntityType != typeof(object))
        {
            var mappings = FlatFileExpressionHelper.GetColumnMappings(source.EntityType);
            if (HasDateTimeColumns(mappings))
            {
                return GenerateViewSqlWithDateTimeCasts(source, mappings);
            }
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
    private string GenerateViewSqlWithDateTimeCasts(IFileSource source, List<ColumnMapping> mappings)
    {
        var readFunction = DuckDbDialect.GenerateReadFunction(source);
        var dateTimeColumns = GetDateTimeColumnNamesFromMappings(mappings);

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

    private static bool HasDateTimeColumns(List<ColumnMapping> mappings)
    {
        foreach (var mapping in mappings)
        {
            if (mapping.IsDateTime) return true;
        }
        return false;
    }

    private static HashSet<string> GetDateTimeColumnNamesFromMappings(List<ColumnMapping> mappings)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in mappings)
        {
            if (mapping.IsDateTime)
                result.Add(mapping.ColumnName);
        }
        return result;
    }

    /// <summary>
    /// Validates that the file's inferred schema is compatible with the entity type.
    /// Checks that every mapped entity property has a corresponding column in the file.
    /// Extra file columns not mapped to entity properties are allowed (SELECT-style semantics).
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

        // Validate that every mapped entity property has a matching file column.
        // Extra file columns are intentionally allowed — the entity only maps the columns it needs.
        foreach (var mapping in FlatFileExpressionHelper.GetColumnMappings(source.EntityType))
        {
            if (!fileColumns.ContainsKey(mapping.ColumnName))
            {
                throw new InvalidOperationException(
                    $"Schema validation failed for '{source.TableName}': Entity property '{mapping.Property.Name}' " +
                    $"maps to column '{mapping.ColumnName}' which does not exist in the file. " +
                    $"Available columns: {string.Join(", ", fileColumns.Keys)}");
            }
        }
    }
}
