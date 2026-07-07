using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// Builder for fluent INSERT operations.
/// </summary>
internal sealed class InsertBuilder<T> : IIntoClause<T>, IValuesClause<T>
    where T : new()
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly EntityMetadata _metadata;
    private readonly CachedDialectMetadata _cache;
    private readonly List<InsertColumn> _columns = new();
    private readonly ParameterCollection _parameters = new();

    internal InsertBuilder(IDbConnection connection)
    {
        _connection = connection;
        _dialect = SqlDialectFactory.GetDialect(connection);
        _metadata = FluentMetadataCache.GetMetadata<T>();
        _cache = FluentMetadataCache.GetForDialect<T>(_dialect);
    }

    #region IIntoClause implementation

    public IValuesClause<T> Values(T entity)
    {
        // Get all insertable columns (non-identity, non-computed)
        IReadOnlyList<ColumnMetadata> columns = _metadata.NonIdentityColumns;
        for (int i = 0; i < columns.Count; i++)
        {
            ColumnMetadata col = columns[i];
            if (!col.IsComputed)
            {
                var value = col.Getter is { } getter ? getter(entity!) : col.Property!.GetValue(entity);
                var paramName = $"{_dialect.ParameterPrefix}{col.PropertyName}";
                _parameters.Add(paramName, value);
                _columns.Add(new InsertColumn(_dialect.EscapeColumnName(col.ColumnName), paramName));
            }
        }
        return this;
    }

    public IValuesClause<T> Values(object values)
    {

        foreach (PropertyInfo? prop in values.GetType().GetProperties())
        {
            string columnName = GetColumnNameFromProperty(prop.Name);

            // Skip identity and computed columns
            ColumnMetadata? colMeta = GetColumnMetadata(prop.Name);
            if (colMeta?.IsIdentity == true || colMeta?.IsComputed == true)
                continue;

            var paramName = $"{_dialect.ParameterPrefix}{prop.Name}";
            _parameters.Add(paramName, prop.GetValue(values));
            _columns.Add(new InsertColumn(_dialect.EscapeColumnName(columnName), paramName));
        }
        return this;
    }

    public IValuesClause<T> Value<TValue>(Expression<Func<T, TValue>> selector, TValue value)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(selector);
        string columnName = GetColumnNameFromProperty(propertyName);
        var paramName = $"{_dialect.ParameterPrefix}{propertyName}";
        _parameters.Add(paramName, value);
        _columns.Add(new InsertColumn(_dialect.EscapeColumnName(columnName), paramName));
        return this;
    }

    public IValuesClause<T> Value(string column, object? value)
    {
        var paramName = $"{_dialect.ParameterPrefix}{column}";
        _parameters.Add(paramName, value);
        _columns.Add(new InsertColumn(_dialect.EscapeColumnName(column), paramName));
        return this;
    }

    #endregion

    #region IValuesClause implementation

    IValuesClause<T> IValuesClause<T>.Value<TValue>(Expression<Func<T, TValue>> selector, TValue value)
    {
        return Value(selector, value);
    }

    IValuesClause<T> IValuesClause<T>.Value(string column, object? value)
    {
        return Value(column, value);
    }

    public long Insert()
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("Insert() requires at least one value to be specified.");

        var sql = BuildInsertSql();
        return ExecuteInsert(sql);
    }

    public async Task<long> InsertAsync(CancellationToken cancellationToken = default)
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("InsertAsync() requires at least one value to be specified.");

        var sql = BuildInsertSql();
        return await ExecuteInsertAsync(sql, cancellationToken).ConfigureAwait(false);
    }

    public string ToSql() => BuildInsertSql();

    #endregion

    #region Private helpers

    private string BuildInsertSql()
    {
        var sb = new StringBuilder(256);
        sb.Append("INSERT INTO ");
        sb.Append(_cache.EscapedTableName);
        sb.Append(" (");

        for (int i = 0; i < _columns.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(_columns[i].ColumnName);
        }

        sb.Append(") VALUES (");

        for (int i = 0; i < _columns.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(_columns[i].ParameterName);
        }

        sb.Append(')');

        return sb.ToString();
    }

    private string BuildInsertWithIdentitySql(string insertSql)
    {
        // The identity-retrieval SQL must run in the same batch/round-trip as the INSERT.
        // Two dialect shapes are supported:
        //   - A RETURNING clause (e.g. PostgreSQL "RETURNING id;") appended directly to
        //     the INSERT VALUES clause before the terminating semicolon.
        //   - A standalone SELECT statement (e.g. SQL Server, MySQL, SQLite) appended as
        //     a second statement in the same command batch after a semicolon.
        string identitySql = _dialect.GetLastInsertIdSql();

        if (identitySql.TrimStart().StartsWith("RETURNING", StringComparison.OrdinalIgnoreCase))
            return $"{insertSql} {identitySql}";

        return $"{insertSql}; {identitySql}";
    }

    private long ExecuteInsert(string sql)
    {
        var wasClosed = _connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed)
                _connection.Open();

            using IDbCommand command = _connection.CreateCommand();
            _parameters.BindTo(command);

            // If there's an identity column, append the identity-retrieval SQL to the
            // same command so it executes in the same batch/round-trip as the INSERT.
            if (HasIdentityColumn())
            {
                command.CommandText = BuildInsertWithIdentitySql(sql);
                var result = command.ExecuteScalar();
                return Convert.ToInt64(result);
            }

            command.CommandText = sql;
            command.ExecuteNonQuery();
            return 1; // 1 row affected
        }
        finally
        {
            if (wasClosed && _connection.State != ConnectionState.Closed)
                _connection.Close();
        }
    }

    private async Task<long> ExecuteInsertAsync(string sql, CancellationToken cancellationToken)
    {
        if (_connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var wasClosed = dbConnection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed)
                await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

            using DbCommand command = dbConnection.CreateCommand();
            _parameters.BindTo(command);

            // If there's an identity column, append the identity-retrieval SQL to the
            // same command so it executes in the same batch/round-trip as the INSERT.
            if (HasIdentityColumn())
            {
                command.CommandText = BuildInsertWithIdentitySql(sql);
                var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return Convert.ToInt64(result);
            }

            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return 1; // 1 row affected
        }
        finally
        {
            if (wasClosed && dbConnection.State != ConnectionState.Closed)
                dbConnection.Close();
        }
    }

    private bool HasIdentityColumn()
    {
        IReadOnlyList<ColumnMetadata> primaryKeys = _metadata.PrimaryKeys;
        return primaryKeys.Count == 1 && primaryKeys[0].IsIdentity;
    }

    private string GetColumnNameFromProperty(string propertyName) => _cache.GetColumnName(propertyName);

    private ColumnMetadata? GetColumnMetadata(string propertyName)
    {
        IReadOnlyList<ColumnMetadata> columns = _metadata.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].PropertyName == propertyName)
                return columns[i];
        }
        return null;
    }

    #endregion
}

/// <summary>
/// Represents a single column for INSERT operations.
/// </summary>
internal readonly struct InsertColumn
{
    public string ColumnName { get; }
    public string ParameterName { get; }

    public InsertColumn(string columnName, string parameterName)
    {
        ColumnName = columnName;
        ParameterName = parameterName;
    }
}