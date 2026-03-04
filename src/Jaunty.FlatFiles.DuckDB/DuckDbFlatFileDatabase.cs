using System.Collections.Concurrent;
using System.Data;
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
