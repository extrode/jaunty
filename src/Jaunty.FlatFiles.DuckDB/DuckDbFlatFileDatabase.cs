using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using DuckDB.NET.Data;
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
            RegisterSource(source);
        }
    }

    /// <inheritdoc />
    public void RegisterSource(IFileSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sql = source.IsPreloaded
            ? _dialect.GenerateCreateTableAsSql(source)
            : _dialect.GenerateCreateViewSql(source);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();

        _sources[source.EntityType] = source;
    }

    /// <inheritdoc />
    public async ValueTask RegisterSourceAsync(IFileSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        var sql = source.IsPreloaded
            ? _dialect.GenerateCreateTableAsSql(source)
            : _dialect.GenerateCreateViewSql(source);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

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
}
