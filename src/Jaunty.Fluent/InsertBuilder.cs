using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Dialects;
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
        _metadata = MetadataCache<T>.Metadata;
        _cache = FluentMetadataCache<T>.GetForDialect(_dialect);
    }

    #region IIntoClause implementation

    public IValuesClause<T> Values(T entity)
    {
        // Get all insertable columns (non-identity, non-computed)
        var columns = _metadata.NonIdentityColumns;
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (!col.IsComputed)
            {
                var value = col.Property.GetValue(entity);
                var paramName = $"@{col.Property.Name}";
                _parameters.Add(paramName, value);
                _columns.Add(new InsertColumn(_dialect.EscapeColumnName(col.ColumnName), paramName));
            }
        }
        return this;
    }

    public IValuesClause<T> Values(object values)
    {
        foreach (var prop in values.GetType().GetProperties())
        {
            string columnName = GetColumnNameFromProperty(prop.Name);

            // Skip identity and computed columns
            var colMeta = GetColumnMetadata(prop.Name);
            if (colMeta?.IsIdentity == true || colMeta?.IsComputed == true)
                continue;

            var paramName = $"@{prop.Name}";
            _parameters.Add(paramName, prop.GetValue(values));
            _columns.Add(new InsertColumn(_dialect.EscapeColumnName(columnName), paramName));
        }
        return this;
    }

    public IValuesClause<T> Value<TValue>(Expression<Func<T, TValue>> selector, TValue value)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(selector);
        string columnName = GetColumnNameFromProperty(propertyName);
        var paramName = $"@{propertyName}";
        _parameters.Add(paramName, value);
        _columns.Add(new InsertColumn(_dialect.EscapeColumnName(columnName), paramName));
        return this;
    }

    public IValuesClause<T> Value(string column, object? value)
    {
        var paramName = $"@{column}";
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

    private long ExecuteInsert(string sql)
    {
        var wasClosed = _connection.State == ConnectionState.Closed;
        try
        {
            if (wasClosed)
                _connection.Open();

            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            _parameters.BindTo(command);

            command.ExecuteNonQuery();

            // If there's an identity column, get the last inserted ID
            if (HasIdentityColumn())
            {
                command.CommandText = _dialect.GetLastInsertIdSql();
                command.Parameters.Clear();
                var result = command.ExecuteScalar();
                return Convert.ToInt64(result);
            }

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

            using var command = dbConnection.CreateCommand();
            command.CommandText = sql;
            _parameters.BindTo(command);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            // If there's an identity column, get the last inserted ID
            if (HasIdentityColumn())
            {
                command.CommandText = _dialect.GetLastInsertIdSql();
                command.Parameters.Clear();
                var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                return Convert.ToInt64(result);
            }

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
        var primaryKeys = _metadata.PrimaryKeys;
        return primaryKeys.Count == 1 && primaryKeys[0].IsIdentity;
    }

    private string GetColumnNameFromProperty(string propertyName) => _cache.GetColumnName(propertyName);

    private ColumnMetadata? GetColumnMetadata(string propertyName)
    {
        var columns = _metadata.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
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
