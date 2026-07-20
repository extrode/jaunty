using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text;

using DuckDB.NET.Data;

using Jaunty.Dialects;
using Jaunty.FlatFiles.Core;
using Jaunty.FlatFiles.DuckDB.Dialects;
using Jaunty.FlatFiles.DuckDB.Internals;
using Jaunty.FlatFiles.DuckDB.Internals.Import;
using Jaunty.FlatFiles.Import;
using Jaunty.FlatFiles.Interfaces;

namespace Jaunty.FlatFiles.DuckDB;

/// <summary>
/// A flat file database backed by DuckDB. Creates an in-memory (or file-backed) DuckDB instance
/// and registers flat file sources as queryable views.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DuckDb"/> is the core implementation of <see cref="IFlatFile"/> that uses DuckDB as the embedded query engine.
/// It provides fast, memory-efficient querying of flat files (CSV, TSV, Parquet, JSON, Excel, Delta Lake, Iceberg)
/// using DuckDB's native read functions.
/// </para>
/// <para>
/// Files are registered as DuckDB views by default (lazy, read-only). On the first mutation operation
/// (INSERT, UPDATE, DELETE), views are automatically promoted to tables. This is transparent to the caller.
/// </para>
/// <para>
/// <b>Thread Safety:</b> DuckDB allows multiple concurrent readers but only one writer. Do not perform
/// concurrent write operations on the same <see cref="DuckDb"/> instance.
/// </para>
/// </remarks>
public sealed partial class DuckDb : IFlatFile
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

        string connectionString = options.DatabasePath == ":memory:"
            ? "DataSource=:memory:"
            : $"DataSource={options.DatabasePath}";

        _connection = new DuckDBConnection(connectionString);

        if (options.RegisterDialect)
            SqlDialectFactory.RegisterDialect("DuckDBConnection", _dialect);

        if (options.AutoOpen)
            _connection.Open();

        try
        {
            foreach (IFileSource source in options.Sources)
            {
                if (options.PreloadIntoMemory && !source.IsPreloaded)
                    source.IsPreloaded = true;

                RegisterSource(source);
            }
        }
        catch
        {
            // A source registration can fail partway through; without this, _connection would
            // leak because the constructor never returns an instance the caller could Dispose.
            _connection.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public void RegisterSource(IFileSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        string sql = GenerateRegistrationSql(source);

        using DuckDBCommand cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();

        if (_options.ValidateSchema && source.EntityType != typeof(object))
            ValidateSchema(source);

        _sources[source.EntityType] = source;
    }

    /// <inheritdoc />
    public async ValueTask RegisterSourceAsync(IFileSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        string sql = await GenerateRegistrationSqlAsync(source, cancellationToken).ConfigureAwait(false);

        DuckDBCommand cmd = _connection.CreateCommand();
        await using var cmdDisposer = cmd.ConfigureAwait(false);
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        if (_options.ValidateSchema && source.EntityType != typeof(object))
            await ValidateSchemaAsync(source, cancellationToken).ConfigureAwait(false);

        _sources[source.EntityType] = source;
    }

    /// <inheritdoc />
    public IFileSource? GetSource<T>() where T : class, new()
    {
        return _sources.TryGetValue(typeof(T), out IFileSource? source) ? source : null;
    }

    /// <inheritdoc />
    public bool IsModified<T>() where T : class, new()
    {
        return _modified.TryGetValue(typeof(T), out bool modified) && modified;
    }

    /// <summary>
    /// Gets the DuckDB dialect instance used by this database.
    /// </summary>
    internal DuckDbDialect Dialect => _dialect;

    private IFileSource GetSourceOrThrow<T>() where T : class, new()
    {
        return !_sources.TryGetValue(typeof(T), out IFileSource? source)
            ? throw new InvalidOperationException(
                $"No file source registered for entity type '{typeof(T).Name}'. " +
                $"Register it via AddCsv<{typeof(T).Name}>(), AddParquet<{typeof(T).Name}>(), etc.")
            : source;
    }

    private string GenerateRegistrationSql(IFileSource source)
    {
        if (source.EntityType != typeof(object))
        {
            IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(source.EntityType);

            if (HasDateTimeColumns(mappings))
                return GenerateViewSqlWithDateTimeCasts(source, mappings);
        }

        return source.IsPreloaded
            ? _dialect.GenerateCreateTableAsSql(source)
            : _dialect.GenerateCreateViewSql(source);
    }

    private async ValueTask<string> GenerateRegistrationSqlAsync(IFileSource source, CancellationToken cancellationToken)
    {
        if (source.EntityType != typeof(object))
        {
            IReadOnlyDictionary<string, ColumnMapping> mappings = ColumnMappingCache.Get(source.EntityType);

            if (HasDateTimeColumns(mappings))
                return await GenerateViewSqlWithDateTimeCastsAsync(source, mappings, cancellationToken).ConfigureAwait(false);
        }

        return source.IsPreloaded
            ? _dialect.GenerateCreateTableAsSql(source)
            : _dialect.GenerateCreateViewSql(source);
    }

    private static bool HasDateTimeColumns(IReadOnlyDictionary<string, ColumnMapping> mappings)
    {
        foreach (ColumnMapping mapping in mappings.Values)
            if (mapping.IsDateTime)
                return true;

        return false;
    }

    private string GenerateViewSqlWithDateTimeCasts(IFileSource source, IReadOnlyDictionary<string, ColumnMapping> mappings)
    {
        string readFunction = DuckDbDialect.GenerateReadFunction(source);
        HashSet<string> dateTimeColumns = GetDateTimeColumnNamesFromMappings(mappings);
        List<string> fileColumns;

        using (DuckDBCommand cmd = _connection.CreateCommand())
        {
            cmd.CommandText = $"SELECT * FROM {readFunction} LIMIT 0";
            using DuckDBDataReader reader = cmd.ExecuteReader();
            fileColumns = new List<string>(reader.FieldCount);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                fileColumns.Add(reader.GetName(i));
            }
        }

        var sb = new StringBuilder();

        for (int i = 0; i < fileColumns.Count; i++)
        {
            if (i > 0) sb.Append(", ");

            string col = fileColumns[i];

            if (dateTimeColumns.Contains(col))
                sb.Append($"CAST(\"{col}\" AS TIMESTAMP) AS \"{col}\"");
            else
                sb.Append($"\"{col}\"");
        }

        string keyword = source.IsPreloaded ? "TABLE" : "VIEW";

        return $"CREATE OR REPLACE {keyword} \"{source.TableName}\" AS SELECT {sb} FROM {readFunction}";
    }

    private async ValueTask<string> GenerateViewSqlWithDateTimeCastsAsync(IFileSource source, IReadOnlyDictionary<string, ColumnMapping> mappings, CancellationToken cancellationToken)
    {
        string readFunction = DuckDbDialect.GenerateReadFunction(source);
        HashSet<string> dateTimeColumns = GetDateTimeColumnNamesFromMappings(mappings);
        List<string> fileColumns;

        DuckDBCommand cmd = _connection.CreateCommand();
        await using (cmd.ConfigureAwait(false))
        {
            cmd.CommandText = $"SELECT * FROM {readFunction} LIMIT 0";
            DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                fileColumns = new List<string>(reader.FieldCount);
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    fileColumns.Add(reader.GetName(i));
                }
            }
        }

        var sb = new StringBuilder();

        for (int i = 0; i < fileColumns.Count; i++)
        {
            if (i > 0) sb.Append(", ");

            string col = fileColumns[i];

            if (dateTimeColumns.Contains(col))
                sb.Append($"CAST(\"{col}\" AS TIMESTAMP) AS \"{col}\"");
            else
                sb.Append($"\"{col}\"");
        }

        string keyword = source.IsPreloaded ? "TABLE" : "VIEW";

        return $"CREATE OR REPLACE {keyword} \"{source.TableName}\" AS SELECT {sb} FROM {readFunction}";
    }

    private static HashSet<string> GetDateTimeColumnNamesFromMappings(IReadOnlyDictionary<string, ColumnMapping> mappings)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ColumnMapping mapping in mappings.Values)
            if (mapping.IsDateTime)
                result.Add(mapping.ColumnName);

        return result;
    }

    private void ValidateSchema(IFileSource source)
    {
        using DuckDBCommand cmd = _connection.CreateCommand();
        cmd.CommandText = $"DESCRIBE \"{source.TableName}\"";

        using DuckDBDataReader reader = cmd.ExecuteReader();
        var fileColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            string colName = reader.GetString(0);
            string colType = reader.GetString(1);
            fileColumns[colName] = colType;
        }

        foreach (ColumnMapping mapping in ColumnMappingCache.Get(source.EntityType).Values)
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

    private async ValueTask ValidateSchemaAsync(IFileSource source, CancellationToken cancellationToken)
    {
        DuckDBCommand cmd = _connection.CreateCommand();
        await using var cmdDisposer = cmd.ConfigureAwait(false);
        cmd.CommandText = $"DESCRIBE \"{source.TableName}\"";

        DbDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var readerDisposer = reader.ConfigureAwait(false);

        var fileColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string colName = reader.GetString(0);
            string colType = reader.GetString(1);
            fileColumns[colName] = colType;
        }

        foreach (ColumnMapping mapping in ColumnMappingCache.Get(source.EntityType).Values)
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

    /// <inheritdoc />
    public async ValueTask<long> ImportIntoAsync<T>(DbConnection targetConnection, ImportOptions options = default, CancellationToken cancellationToken = default) where T : class, new()
    {
        ArgumentNullException.ThrowIfNull(targetConnection);
        IFileSource source = GetSourceOrThrow<T>();

        return await ImportExecutor.ExecuteAsync<T>(_connection, source, targetConnection, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
