using System.Data;
using System.Globalization;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Parameters;
using Jaunty.Internals;

using Jaunty.Core;

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
        ParameterMetadata[] props = ParameterCache.Get(values.GetType());
        for (int i = 0; i < props.Length; i++)
        {
            ParameterMetadata prop = props[i];

            // GetColumnNameFromProperty already returns the dialect-escaped column name (it's
            // backed by CachedDialectMetadata, whose cached values are pre-escaped) - do not
            // escape it again here, or a keyword-named column (e.g. "[Order]") fails
            // SqlIdentifierValidator's plain-identifier check on the second pass.
            string columnName = GetColumnNameFromProperty(prop.Name);

            // Skip identity and computed columns. AUD-R22: a property that doesn't correspond to
            // a mapped column on T (colMeta is null) is intentionally allowed through - see
            // KeywordColumnEscapingTests.InsertValues_AnonymousObject_WithUnmappedKeywordPropertyName_EscapesColumn
            // (R14 batch-5): callers can insert into columns not present on the mapped entity type.
            ColumnMetadata? colMeta = GetColumnMetadata(prop.Name);
            if (colMeta is not null && (colMeta.IsIdentity || colMeta.IsComputed))
                continue;

            var paramName = $"{_dialect.ParameterPrefix}{prop.Name}";
            _parameters.Add(paramName, prop.Getter(values));
            _columns.Add(new InsertColumn(columnName, paramName));
        }
        return this;
    }

    public IValuesClause<T> Value<TValue>(Expression<Func<T, TValue>> selector, TValue value)
    {
        string propertyName = PropertyExtractor.ExtractPropertyName(selector);
        // Already escaped - see comment in Values(object) above.
        string columnName = GetColumnNameFromProperty(propertyName);
        var paramName = $"{_dialect.ParameterPrefix}{propertyName}";
        _parameters.Add(paramName, value);
        _columns.Add(new InsertColumn(columnName, paramName));
        return this;
    }

    public IValuesClause<T> Value(string column, object? value)
    {
        var paramName = $"{_dialect.ParameterPrefix}{SanitizeParamName(column)}";
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

    public long Insert() => Insert(default);

    /// <summary>
    /// Executes the insert, enlisting it in <paramref name="options"/>'s transaction and honouring
    /// its command timeout.
    /// </summary>
    /// <remarks>
    /// AUD-R26-060: before this overload existed there was no way to pass a transaction to a fluent
    /// insert at all - <c>IIntoClause</c>/<c>IValuesClause</c> declared no options overload and
    /// <c>ExecuteInsert</c> took no options parameter - while <c>QueryBuilder.Delete</c>,
    /// <c>Update</c>, <c>Select</c> and friends all carried one.
    /// </remarks>
    public long Insert(CommandOptions options)
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("Insert() requires at least one value to be specified.");

        var sql = BuildInsertSql();
        return ExecuteInsert(sql, options);
    }

    public Task<long> InsertAsync(CancellationToken cancellationToken = default) =>
        InsertAsync(default, cancellationToken);

    /// <inheritdoc cref="Insert(CommandOptions)"/>
    public async Task<long> InsertAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("InsertAsync() requires at least one value to be specified.");

        var sql = BuildInsertSql();
        return await ExecuteInsertAsync(sql, options, cancellationToken).ConfigureAwait(false);
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
        //   - A RETURNING clause (e.g. PostgreSQL "RETURNING <column>;") appended directly to
        //     the INSERT VALUES clause before the terminating semicolon. Unlike SCOPE_IDENTITY()/
        //     LAST_INSERT_ID()/last_insert_rowid(), Postgres has no dialect-wide "last id"
        //     function - RETURNING needs the actual identity column name, so it must be passed
        //     here rather than calling the no-args overload (which hardcodes "id").
        //   - A standalone SELECT statement (e.g. SQL Server, MySQL, SQLite) appended as
        //     a second statement in the same command batch after a semicolon; these dialects
        //     ignore the column name argument entirely, so passing it is harmless for them.
        string identityColumnName = _dialect.EscapeColumnName(_metadata.PrimaryKeys[0].ColumnName);
        string identitySql = _dialect.GetLastInsertIdSql(identityColumnName);

        if (identitySql.TrimStart().StartsWith("RETURNING", StringComparison.OrdinalIgnoreCase))
            return $"{insertSql} {identitySql}";

        return $"{insertSql}; {identitySql}";
    }

    private long ExecuteInsert(string sql, CommandOptions options)
    {
        // If there's an identity column, the identity-retrieval SQL is appended to the same
        // command so it executes in the same batch/round-trip as the INSERT. Deciding that up
        // front means what is reported to interceptors is what actually executes.
        bool hasIdentity = HasIdentityColumn();
        string commandText = hasIdentity ? BuildInsertWithIdentitySql(sql) : sql;

        return CommandObservation.Execute(
            commandText, _parameters.ToParameterObject(), _connection, FluentCommandOptions.Describe(options), Body);

        long Body()
        {
            var wasClosed = _connection.State == ConnectionState.Closed;
            try
            {
                if (wasClosed)
                    _connection.Open();

                using IDbCommand command = _connection.CreateCommand();
                _parameters.BindTo(command);
                command.CommandText = commandText;
                FluentCommandOptions.Apply(command, _connection, options);

                CommandObservation.Log(commandText, _parameters.ToParameterObject());

                if (hasIdentity)
                {
                    var result = command.ExecuteScalar();
                    return Convert.ToInt64(result, CultureInfo.InvariantCulture);
                }

                command.ExecuteNonQuery();
                return 1; // 1 row affected
            }
            finally
            {
                if (wasClosed && _connection.State != ConnectionState.Closed)
                    _connection.Close();
            }
        }
    }

    private async Task<long> ExecuteInsertAsync(string sql, CommandOptions options, CancellationToken cancellationToken)
    {
        if (_connection is not DbConnection dbConnection)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        bool hasIdentity = HasIdentityColumn();
        string commandText = hasIdentity ? BuildInsertWithIdentitySql(sql) : sql;

        return await CommandObservation.ExecuteAsync(
            commandText, _parameters.ToParameterObject(), _connection, FluentCommandOptions.Describe(options),
            Body, cancellationToken).ConfigureAwait(false);

        async ValueTask<long> Body()
        {
            var wasClosed = dbConnection.State == ConnectionState.Closed;
            try
            {
                if (wasClosed)
                    await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

                using DbCommand command = dbConnection.CreateCommand();
                _parameters.BindTo(command);
                command.CommandText = commandText;
                FluentCommandOptions.Apply(command, dbConnection, options);

                CommandObservation.Log(commandText, _parameters.ToParameterObject());

                if (hasIdentity)
                {
                    var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    return Convert.ToInt64(result, CultureInfo.InvariantCulture);
                }

                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return 1; // 1 row affected
            }
            finally
            {
                if (wasClosed && dbConnection.State != ConnectionState.Closed)
                    dbConnection.Close();
            }
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

    // AUD-R22: column is the raw caller-supplied column name from the string-based Value
    // overload. A space or other character invalid in a SQL parameter identifier (e.g.
    // Value("Order Date", value)) used to be interpolated unsanitized, producing a malformed
    // placeholder that fails at execution time.
    private static string SanitizeParamName(string name)
    {
        char[] chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                chars[i] = '_';
        }
        return new string(chars);
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