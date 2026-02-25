using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;

using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals;
using Jaunty.Internals.Dialects;
using Jaunty.Internals.Entity;
using Jaunty.Internals.Enums;
using Jaunty.Internals.Read;

namespace Jaunty.Fluent;

/// <summary>
/// Query builder for joined queries. Implements IJoinedQuery.
/// </summary>
internal sealed class JoinedQueryBuilder<TFrom, TJoin> : IJoinedQuery<TFrom, TJoin> where TFrom : new() where TJoin : new()
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly string _fromTable;
    private readonly string? _fromSchema;
    private readonly string? _fromAlias;
    private readonly List<JoinInfo> _joins = new();
    private readonly List<WhereCondition> _conditions = new();
    private readonly List<OrderByColumn> _orderByColumns = new();
    private readonly ParameterCollection _parameters = new();
    private readonly EntityMetadata _fromMetadata;
    private readonly EntityMetadata _joinMetadata;

    internal JoinedQueryBuilder(IDbConnection connection, ISqlDialect dialect, string fromTable, string? fromSchema, string? fromAlias, JoinInfo firstJoin)
    {
        _connection = connection;
        _dialect = dialect;
        _fromTable = fromTable;
        _fromSchema = fromSchema;
        _fromAlias = fromAlias;
        _joins.Add(firstJoin);
        _fromMetadata = FluentMetadataCache.GetMetadata<TFrom>();
        _joinMetadata = FluentMetadataCache.GetMetadata<TJoin>();
    }

    internal string? FromAlias => _fromAlias;
    internal string FromTable => _fromTable;
    internal string? FromSchema => _fromSchema;
    internal ISqlDialect Dialect => _dialect;
    internal IDbConnection Connection => _connection;
    internal List<JoinInfo> Joins => _joins;

    private string JoinAlias => _joins[0].Alias ?? _joinMetadata.TableName;

    #region Additional Joins

    public IJoinClause<TFrom, TJoin, T3> InnerJoin<T3>(string? alias = null) where T3 : new()
    {
        return new JoinClause3Builder<TFrom, TJoin, T3>(this, JoinType.Inner, alias);
    }

    public IJoinClause<TFrom, TJoin, T3> LeftJoin<T3>(string? alias = null) where T3 : new()
    {
        return new JoinClause3Builder<TFrom, TJoin, T3>(this, JoinType.Left, alias);
    }

    #endregion

    #region WHERE

    public IJoinedQuery<TFrom, TJoin> Where(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(
            _dialect,
            _fromAlias,
            _joins[0].Alias);

        var sql = visitor.Translate(predicate);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> Where(string condition)
    {
        _conditions.Add(WhereCondition.Raw(condition, LogicalOperator.None));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> Where(string column, object value)
    {
        var paramName = $"@{column.Replace(".", "_")}";
        _conditions.Add(WhereCondition.Column($"{column} = {paramName}", LogicalOperator.None));
        _parameters.Add(paramName, value);
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> And(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(
            _dialect,
            _fromAlias,
            _joins[0].Alias);

        var sql = visitor.Translate(predicate);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.And));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> Or(Expression<Func<TFrom, TJoin, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor<TFrom, TJoin>(
            _dialect,
            _fromAlias,
            _joins[0].Alias);

        var sql = visitor.Translate(predicate);
        _conditions.Add(WhereCondition.Expression(sql, LogicalOperator.Or));
        return this;
    }

    #endregion

    #region ORDER BY

    public IJoinedQuery<TFrom, TJoin> OrderBy<TKey>(Expression<Func<TFrom, TKey>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        var columnName = GetColumnName(_fromMetadata, propertyName, _fromAlias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> OrderByJoined<TKey>(Expression<Func<TJoin, TKey>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        var columnName = GetColumnName(_joinMetadata, propertyName, _joins[0].Alias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> OrderByDescending<TKey>(Expression<Func<TFrom, TKey>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        var columnName = GetColumnName(_fromMetadata, propertyName, _fromAlias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> OrderByJoinedDescending<TKey>(Expression<Func<TJoin, TKey>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        var columnName = GetColumnName(_joinMetadata, propertyName, _joins[0].Alias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> ThenBy<TKey>(Expression<Func<TFrom, TKey>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        var columnName = GetColumnName(_fromMetadata, propertyName, _fromAlias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> ThenByJoined<TKey>(Expression<Func<TJoin, TKey>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        var columnName = GetColumnName(_joinMetadata, propertyName, _joins[0].Alias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> ThenByDescending<TKey>(Expression<Func<TFrom, TKey>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        var columnName = GetColumnName(_fromMetadata, propertyName, _fromAlias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    public IJoinedQuery<TFrom, TJoin> ThenByJoinedDescending<TKey>(Expression<Func<TJoin, TKey>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractPropertyName(keySelector);
        var columnName = GetColumnName(_joinMetadata, propertyName, _joins[0].Alias);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    #endregion

    #region SELECT - Typed Selection

    /// <summary>
    /// Selects the primary (From) entity.
    /// </summary>
    public List<TFrom> Select()
    {
        var columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        var sql = BuildSelectSql(columns);
        return _connection.QueryPartial<TFrom>(sql, _parameters.ToParameterObject()!);
    }

    /// <summary>
    /// Selects the specified entity type. Uses IMapped&lt;T&gt; if implemented, otherwise strict mapping.
    /// </summary>
    public List<T> Select<T>() where T : new()
    {
        // Optimized paths for TFrom and TJoin
        if (typeof(T) == typeof(TFrom))
        {
            var result = Select();
            return Unsafe.As<List<TFrom>, List<T>>(ref result);
        }
        if (typeof(T) == typeof(TJoin))
        {
            var result = SelectJoinedInternal();
            return Unsafe.As<List<TJoin>, List<T>>(ref result);
        }

        // For other types, use standard Jaunty mapping (IMapped<T> or strict reflection)
        return SelectWithMapping<T>(MappingMode.Strict);
    }

    /// <summary>
    /// Selects using a custom mapper.
    /// </summary>
    public List<T> Select<T>(Func<IDataReader, T> mapper)
    {
        return SelectWithMapper(mapper);
    }

    /// <summary>
    /// Selects both entities as tuples.
    /// </summary>
    public List<(TFrom From, TJoin Joined)> SelectBoth()
    {
        return SelectBothInternal();
    }

    /// <summary>
    /// Selects both entities as tuples.
    /// </summary>
    public List<(T1, T2)> Select<T1, T2>() where T1 : new() where T2 : new()
    {
        // Validate types match at runtime
        if (typeof(T1) != typeof(TFrom))
            throw new ArgumentException($"T1 must be {typeof(TFrom).Name}, got {typeof(T1).Name}", nameof(T1));
        if (typeof(T2) != typeof(TJoin))
            throw new ArgumentException($"T2 must be {typeof(TJoin).Name}, got {typeof(T2).Name}", nameof(T2));

        var result = SelectBothInternal();
        return Unsafe.As<List<(TFrom, TJoin)>, List<(T1, T2)>>(ref result);
    }

    #endregion

    #region SELECT FIRST - Typed Selection

    public TFrom SelectFirst()
    {
        var columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        var sql = BuildSelectSql(columns) + " LIMIT 1";
        return _connection.QueryPartialFirst<TFrom>(sql, _parameters.ToParameterObject()!);
    }

    public TFrom? SelectFirstOrDefault()
    {
        var columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        var sql = BuildSelectSql(columns) + " LIMIT 1";
        return _connection.QueryPartialFirstOrDefault<TFrom>(sql, _parameters.ToParameterObject()!);
    }

    public T SelectFirst<T>() where T : new()
    {
        // Optimized paths for TFrom and TJoin
        if (typeof(T) == typeof(TFrom))
        {
            var result = SelectFirst();
            return Unsafe.As<TFrom, T>(ref result);
        }
        if (typeof(T) == typeof(TJoin))
        {
            var result = SelectFirstJoinedInternal();
            return Unsafe.As<TJoin, T>(ref result);
        }

        // For other types, use standard Jaunty mapping
        var results = SelectWithMapping<T>(MappingMode.Strict, limit: 1);
        if (results.Count == 0)
            throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
        return results[0];
    }

    public T SelectFirst<T>(Func<IDataReader, T> mapper)
    {
        var results = SelectWithMapper(mapper, limit: 1);
        if (results.Count == 0)
            throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
        return results[0];
    }

    public T? SelectFirstOrDefault<T>() where T : new()
    {
        // Optimized paths for TFrom and TJoin
        if (typeof(T) == typeof(TFrom))
        {
            var result = SelectFirstOrDefault();
            return Unsafe.As<TFrom?, T?>(ref result);
        }
        if (typeof(T) == typeof(TJoin))
        {
            var result = SelectFirstOrDefaultJoinedInternal();
            return Unsafe.As<TJoin?, T?>(ref result);
        }

        // For other types, use standard Jaunty mapping
        var results = SelectWithMapping<T>(MappingMode.Strict, limit: 1);
        return results.Count > 0 ? results[0] : default;
    }

    public T? SelectFirstOrDefault<T>(Func<IDataReader, T> mapper)
    {
        var results = SelectWithMapper(mapper, limit: 1);
        return results.Count > 0 ? results[0] : default;
    }

    public (TFrom From, TJoin Joined) SelectFirstBoth()
    {
        var result = SelectBothInternal();
        if (result.Count == 0)
            throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(TFrom).Name}, {typeof(TJoin).Name})'.");
        return result[0];
    }

    #endregion

    #region Internal Helpers

    internal void BindParameters(IDbCommand command)
    {
        var paramObj = _parameters.ToParameterObject();
        if (paramObj is IDictionary<string, object?> dict)
        {
            foreach (var kvp in dict)
            {
                var p = command.CreateParameter();
                p.ParameterName = kvp.Key;
                p.Value = kvp.Value ?? DBNull.Value;
                command.Parameters.Add(p);
            }
        }
    }

    internal static TEntity MapEntity<TEntity>(EntityMetadata metadata, IDataReader reader, string prefix) where TEntity : new()
    {
        var entity = new TEntity();
        var columns = metadata.Columns;

        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            var aliasName = $"{prefix}{col.ColumnName}";

            try
            {
                var ordinal = reader.GetOrdinal(aliasName);
                if (!reader.IsDBNull(ordinal))
                {
                    var value = reader.GetValue(ordinal);
                    var propertyType = col.Property.PropertyType;

                    // Handle nullable types
                    var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
                    var convertedValue = Convert.ChangeType(value, targetType);
                    col.Property.SetValue(entity, convertedValue);
                }
            }
            catch (IndexOutOfRangeException)
            {
                // Column not found, skip
            }
        }

        return entity;
    }

    internal string BuildSelectSql(string[] columns)
    {
        var sb = new StringBuilder(256);
        sb.Append("SELECT ");

        for (int i = 0; i < columns.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(columns[i]);
        }

        sb.Append(" FROM ");
        sb.Append(_dialect.EscapeTableName(_fromSchema, _fromTable));
        if (_fromAlias is not null)
        {
            sb.Append(' ');
            sb.Append(_fromAlias);
        }

        foreach (var join in _joins)
        {
            sb.Append(' ');
            sb.Append(join.JoinKeyword);
            sb.Append(' ');
            sb.Append(_dialect.EscapeTableName(join.SchemaName, join.TableName));
            if (join.Alias is not null)
            {
                sb.Append(' ');
                sb.Append(join.Alias);
            }
            sb.Append(" ON ");
            sb.Append(join.OnCondition);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            for (int i = 0; i < _conditions.Count; i++)
            {
                var condition = _conditions[i];
                if (i > 0)
                {
                    sb.Append(condition.Operator == LogicalOperator.Or ? " OR " : " AND ");
                }
                sb.Append(condition.Sql);
            }
        }

        if (_orderByColumns.Count > 0)
        {
            sb.Append(" ORDER BY ");
            for (int i = 0; i < _orderByColumns.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var orderBy = _orderByColumns[i];
                sb.Append(orderBy.ColumnName);
                if (orderBy.Descending)
                    sb.Append(" DESC");
            }
        }

        return sb.ToString();
    }

    internal string[] GetPrefixedColumnsWithAlias(EntityMetadata metadata, string? tableAlias, string columnPrefix)
    {
        var columns = metadata.Columns;
        var result = new string[columns.Count];
        var prefix = tableAlias ?? metadata.TableName;

        for (int i = 0; i < columns.Count; i++)
        {
            var colName = columns[i].ColumnName;
            var escaped = _dialect.EscapeColumnName(colName);
            result[i] = $"{prefix}.{escaped} AS {columnPrefix}{colName}";
        }

        return result;
    }

    private List<TJoin> SelectJoinedInternal()
    {
        var columns = GetPrefixedColumns(_joinMetadata, _joins[0].Alias);
        var sql = BuildSelectSql(columns);
        return _connection.QueryPartial<TJoin>(sql, _parameters.ToParameterObject()!);
    }

    private TJoin SelectFirstJoinedInternal()
    {
        var columns = GetPrefixedColumns(_joinMetadata, _joins[0].Alias);
        var sql = BuildSelectSql(columns) + " LIMIT 1";
        return _connection.QueryPartialFirst<TJoin>(sql, _parameters.ToParameterObject()!);
    }

    private TJoin? SelectFirstOrDefaultJoinedInternal()
    {
        var columns = GetPrefixedColumns(_joinMetadata, _joins[0].Alias);
        var sql = BuildSelectSql(columns) + " LIMIT 1";
        return _connection.QueryPartialFirstOrDefault<TJoin>(sql, _parameters.ToParameterObject()!);
    }

    private List<(TFrom From, TJoin Joined)> SelectBothInternal()
    {
        var fromColumns = GetPrefixedColumnsWithAlias(_fromMetadata, _fromAlias, "f_");
        var joinColumns = GetPrefixedColumnsWithAlias(_joinMetadata, _joins[0].Alias, "j_");
        var allColumns = fromColumns.Concat(joinColumns).ToArray();

        var sql = BuildSelectSql(allColumns);
        var results = new List<(TFrom, TJoin)>();

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) _connection.Open();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var fromObj = MapEntity<TFrom>(_fromMetadata, reader, "f_");
                var joinObj = MapEntity<TJoin>(_joinMetadata, reader, "j_");
                results.Add((fromObj, joinObj));
            }
        }
        finally
        {
            if (wasClosed) _connection.Close();
        }

        return results;
    }

    private List<T> SelectWithMapping<T>(MappingMode mode, int? limit = null) where T : new()
    {
        var sql = BuildSelectAllColumnsSql();
        if (limit.HasValue)
            sql += $" LIMIT {limit.Value}";

        var results = new List<T>();

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) _connection.Open();
        try
        {
            using var reader = command.ExecuteReader();
            var mapper = DrDispatcher.Resolve<T>(reader, default, mode);
            while (reader.Read())
            {
                results.Add(mapper(reader));
            }
        }
        finally
        {
            if (wasClosed) _connection.Close();
        }

        return results;
    }

    private List<T> SelectWithMapper<T>(Func<IDataReader, T> mapper, int? limit = null)
    {
        var sql = BuildSelectAllColumnsSql();
        if (limit.HasValue)
            sql += $" LIMIT {limit.Value}";

        var results = new List<T>();

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) _connection.Open();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(mapper(reader));
            }
        }
        finally
        {
            if (wasClosed) _connection.Close();
        }

        return results;
    }

    private string BuildSelectAllColumnsSql()
    {
        var sb = new StringBuilder(256);
        sb.Append("SELECT *");

        sb.Append(" FROM ");
        sb.Append(_dialect.EscapeTableName(_fromSchema, _fromTable));
        if (_fromAlias is not null)
        {
            sb.Append(' ');
            sb.Append(_fromAlias);
        }

        foreach (var join in _joins)
        {
            sb.Append(' ');
            sb.Append(join.JoinKeyword);
            sb.Append(' ');
            sb.Append(_dialect.EscapeTableName(join.SchemaName, join.TableName));
            if (join.Alias is not null)
            {
                sb.Append(' ');
                sb.Append(join.Alias);
            }
            sb.Append(" ON ");
            sb.Append(join.OnCondition);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            for (int i = 0; i < _conditions.Count; i++)
            {
                var condition = _conditions[i];
                if (i > 0)
                {
                    sb.Append(condition.Operator == LogicalOperator.Or ? " OR " : " AND ");
                }
                sb.Append(condition.Sql);
            }
        }

        if (_orderByColumns.Count > 0)
        {
            sb.Append(" ORDER BY ");
            for (int i = 0; i < _orderByColumns.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var orderBy = _orderByColumns[i];
                sb.Append(orderBy.ColumnName);
                if (orderBy.Descending)
                    sb.Append(" DESC");
            }
        }

        return sb.ToString();
    }

    private string[] GetPrefixedColumns(EntityMetadata metadata, string? alias)
    {
        var columns = metadata.Columns;
        var result = new string[columns.Count];
        var prefix = alias ?? metadata.TableName;

        for (int i = 0; i < columns.Count; i++)
        {
            var escaped = _dialect.EscapeColumnName(columns[i].ColumnName);
            result[i] = $"{prefix}.{escaped}";
        }

        return result;
    }

    #endregion

    #region COUNT

    public int Count()
    {
        var sql = BuildCountSql();
        return _connection.QueryScalar<int>(sql, _parameters.ToParameterObject()!);
    }

    public long LongCount()
    {
        var sql = BuildCountSql();
        return _connection.QueryScalar<long>(sql, _parameters.ToParameterObject()!);
    }

    #endregion

    #region SQL Introspection

    public string ToSql()
    {
        var columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        return BuildSelectSql(columns);
    }

    #endregion

    #region Async Operations

    public async Task<List<TFrom>> SelectAsync(CancellationToken cancellationToken = default)
    {
        var columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        var sql = BuildSelectSql(columns);
        if (_connection is DbConnection dbConn)
            return await dbConn.QueryPartialAsync<TFrom>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false);
        return Select();
    }

    public async Task<List<T>> SelectAsync<T>(CancellationToken cancellationToken = default) where T : new()
    {
        if (typeof(T) == typeof(TFrom))
        {
            var result = await SelectAsync(cancellationToken).ConfigureAwait(false);
            return Unsafe.As<List<TFrom>, List<T>>(ref result);
        }
        if (typeof(T) == typeof(TJoin))
        {
            var result = await SelectJoinedInternalAsync(cancellationToken).ConfigureAwait(false);
            return Unsafe.As<List<TJoin>, List<T>>(ref result);
        }

        return await Task.Run(() => SelectWithMapping<T>(MappingMode.Strict), cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<T>> SelectAsync<T>(Func<IDataReader, T> mapper, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectWithMapper(mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<(T1, T2)>> SelectAsync<T1, T2>(CancellationToken cancellationToken = default) where T1 : new() where T2 : new()
    {
        if (typeof(T1) != typeof(TFrom))
            throw new ArgumentException($"T1 must be {typeof(TFrom).Name}, got {typeof(T1).Name}", nameof(T1));
        if (typeof(T2) != typeof(TJoin))
            throw new ArgumentException($"T2 must be {typeof(TJoin).Name}, got {typeof(T2).Name}", nameof(T2));

        var result = await SelectBothInternalAsync(cancellationToken).ConfigureAwait(false);
        return Unsafe.As<List<(TFrom, TJoin)>, List<(T1, T2)>>(ref result);
    }

    public async Task<TFrom> SelectFirstAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectFirst(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<TFrom?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectFirstOrDefault(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectFirstAsync<T>(CancellationToken cancellationToken = default) where T : new()
    {
        return await Task.Run(() => SelectFirst<T>(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectFirstAsync<T>(Func<IDataReader, T> mapper, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectFirst(mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectFirstOrDefaultAsync<T>(CancellationToken cancellationToken = default) where T : new()
    {
        return await Task.Run(() => SelectFirstOrDefault<T>(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectFirstOrDefaultAsync<T>(Func<IDataReader, T> mapper, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectFirstOrDefault(mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<(TFrom From, TJoin Joined)> SelectFirstBothAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectFirstBoth(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => Count(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => LongCount(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<TJoin>> SelectJoinedInternalAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectJoinedInternal(), cancellationToken).ConfigureAwait(false);
    }

    private async Task<List<(TFrom From, TJoin Joined)>> SelectBothInternalAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectBothInternal(), cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region SELECT PARTIAL

    public List<dynamic> SelectPartial(string columns)
    {
        var sql = BuildPartialSelectSql(columns);
        var results = new List<dynamic>();

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) _connection.Open();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(MapToDynamic(reader));
            }
        }
        finally
        {
            if (wasClosed) _connection.Close();
        }

        return results;
    }

    public List<T> SelectPartial<T>(string columns, Func<IDataReader, T> mapper)
    {
        var sql = BuildPartialSelectSql(columns);
        var results = new List<T>();

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) _connection.Open();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(mapper(reader));
            }
        }
        finally
        {
            if (wasClosed) _connection.Close();
        }

        return results;
    }

    public dynamic SelectPartialFirst(string columns)
    {
        var result = SelectPartialFirstOrDefault(columns);
        if (result is null)
            throw new InvalidOperationException("Sequence contains no elements of type 'dynamic'.");
        return result;
    }

    public T SelectPartialFirst<T>(string columns, Func<IDataReader, T> mapper)
    {
        var result = SelectPartialFirstOrDefault(columns, mapper);
        if (result is null)
            throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
        return result;
    }

    public dynamic? SelectPartialFirstOrDefault(string columns)
    {
        var sql = BuildPartialSelectSql(columns) + " LIMIT 1";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) _connection.Open();
        try
        {
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return MapToDynamic(reader);
            }
            return null;
        }
        finally
        {
            if (wasClosed) _connection.Close();
        }
    }

    public T? SelectPartialFirstOrDefault<T>(string columns, Func<IDataReader, T> mapper)
    {
        var sql = BuildPartialSelectSql(columns) + " LIMIT 1";

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) _connection.Open();
        try
        {
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return mapper(reader);
            }
            return default;
        }
        finally
        {
            if (wasClosed) _connection.Close();
        }
    }

    public dynamic SelectPartialSingle(string columns)
    {
        var result = SelectPartialSingleOrDefault(columns);
        if (result is null)
            throw new InvalidOperationException("Sequence contains no elements of type 'dynamic'.");
        return result;
    }

    public T SelectPartialSingle<T>(string columns, Func<IDataReader, T> mapper)
    {
        var result = SelectPartialSingleOrDefault(columns, mapper);
        if (result is null)
            throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.");
        return result;
    }

    public dynamic? SelectPartialSingleOrDefault(string columns)
    {
        var sql = BuildPartialSelectSql(columns) + " LIMIT 2";
        dynamic? result = null;
        int count = 0;

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) _connection.Open();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                count++;
                if (count > 1)
                    throw new InvalidOperationException("Sequence contains more than one element of type 'dynamic'.");
                result = MapToDynamic(reader);
            }
        }
        finally
        {
            if (wasClosed) _connection.Close();
        }

        return result;
    }

    public T? SelectPartialSingleOrDefault<T>(string columns, Func<IDataReader, T> mapper)
    {
        var sql = BuildPartialSelectSql(columns) + " LIMIT 2";
        T? result = default;
        int count = 0;

        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        BindParameters(command);

        var wasClosed = _connection.State == ConnectionState.Closed;
        if (wasClosed) _connection.Open();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                count++;
                if (count > 1)
                    throw new InvalidOperationException($"Sequence contains more than one element of type '{typeof(T).Name}'.");
                result = mapper(reader);
            }
        }
        finally
        {
            if (wasClosed) _connection.Close();
        }

        return result;
    }

    // Async variants

    public async Task<List<dynamic>> SelectPartialAsync(string columns, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartial(columns), cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<T>> SelectPartialAsync<T>(string columns, Func<IDataReader, T> mapper, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartial(columns, mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<dynamic> SelectPartialFirstAsync(string columns, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialFirst(columns), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectPartialFirstAsync<T>(string columns, Func<IDataReader, T> mapper, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialFirst(columns, mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<dynamic?> SelectPartialFirstOrDefaultAsync(string columns, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialFirstOrDefault(columns), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectPartialFirstOrDefaultAsync<T>(string columns, Func<IDataReader, T> mapper, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialFirstOrDefault(columns, mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<dynamic> SelectPartialSingleAsync(string columns, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialSingle(columns), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectPartialSingleAsync<T>(string columns, Func<IDataReader, T> mapper, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialSingle(columns, mapper), cancellationToken).ConfigureAwait(false);
    }

    public async Task<dynamic?> SelectPartialSingleOrDefaultAsync(string columns, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialSingleOrDefault(columns), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectPartialSingleOrDefaultAsync<T>(string columns, Func<IDataReader, T> mapper, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() => SelectPartialSingleOrDefault(columns, mapper), cancellationToken).ConfigureAwait(false);
    }

    #endregion

    internal void AddWhereCondition(WhereCondition condition) => _conditions.Add(condition);

    internal void AddJoin(JoinInfo join) => _joins.Add(join);

    internal void AddParameter<TValue>(string name, TValue value)
    {
        _parameters.Add(name, value);
    }

    #region Private Helpers

    private string BuildPartialSelectSql(string columns)
    {
        var sb = new StringBuilder(256);
        sb.Append("SELECT ");
        sb.Append(columns);

        sb.Append(" FROM ");
        sb.Append(_dialect.EscapeTableName(_fromSchema, _fromTable));
        if (_fromAlias is not null)
        {
            sb.Append(' ');
            sb.Append(_fromAlias);
        }

        foreach (var join in _joins)
        {
            sb.Append(' ');
            sb.Append(join.JoinKeyword);
            sb.Append(' ');
            sb.Append(_dialect.EscapeTableName(join.SchemaName, join.TableName));
            if (join.Alias is not null)
            {
                sb.Append(' ');
                sb.Append(join.Alias);
            }
            sb.Append(" ON ");
            sb.Append(join.OnCondition);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            for (int i = 0; i < _conditions.Count; i++)
            {
                var condition = _conditions[i];
                if (i > 0)
                {
                    sb.Append(condition.Operator == LogicalOperator.Or ? " OR " : " AND ");
                }
                sb.Append(condition.Sql);
            }
        }

        if (_orderByColumns.Count > 0)
        {
            sb.Append(" ORDER BY ");
            for (int i = 0; i < _orderByColumns.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var orderBy = _orderByColumns[i];
                sb.Append(orderBy.ColumnName);
                if (orderBy.Descending)
                    sb.Append(" DESC");
            }
        }

        return sb.ToString();
    }

    private static dynamic MapToDynamic(IDataReader reader)
    {
        var expando = new ExpandoObject() as IDictionary<string, object?>;
        for (int i = 0; i < reader.FieldCount; i++)
        {
            var name = reader.GetName(i);
            var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
            expando[name] = value;
        }
        return (ExpandoObject)expando;
    }

    private string BuildCountSql()
    {
        var sb = new StringBuilder(128);
        sb.Append("SELECT COUNT(*)");

        sb.Append(" FROM ");
        sb.Append(_dialect.EscapeTableName(_fromSchema, _fromTable));
        if (_fromAlias is not null)
        {
            sb.Append(' ');
            sb.Append(_fromAlias);
        }

        foreach (var join in _joins)
        {
            sb.Append(' ');
            sb.Append(join.JoinKeyword);
            sb.Append(' ');
            sb.Append(_dialect.EscapeTableName(join.SchemaName, join.TableName));
            if (join.Alias is not null)
            {
                sb.Append(' ');
                sb.Append(join.Alias);
            }
            sb.Append(" ON ");
            sb.Append(join.OnCondition);
        }

        if (_conditions.Count > 0)
        {
            sb.Append(" WHERE ");
            for (int i = 0; i < _conditions.Count; i++)
            {
                var condition = _conditions[i];
                if (i > 0)
                {
                    sb.Append(condition.Operator == LogicalOperator.Or ? " OR " : " AND ");
                }
                sb.Append(condition.Sql);
            }
        }

        return sb.ToString();
    }

    private string GetColumnName(EntityMetadata metadata, string propertyName, string? alias)
    {
        var columns = metadata.Columns;
        string columnName = propertyName;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
            {
                columnName = columns[i].ColumnName;
                break;
            }
        }

        var escaped = _dialect.EscapeColumnName(columnName);
        var prefix = alias ?? metadata.TableName;
        return $"{prefix}.{escaped}";
    }

    #endregion
}

/// <summary>
/// Builder for third join clause.
/// </summary>
internal sealed class JoinClause3Builder<T1, T2, T3> : IJoinClause<T1, T2, T3>
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    private readonly JoinedQueryBuilder<T1, T2> _parent;
    private readonly JoinType _joinType;
    private readonly string? _alias;
    private readonly EntityMetadata _metadata;

    public JoinClause3Builder(JoinedQueryBuilder<T1, T2> parent, JoinType joinType, string? alias)
    {
        _parent = parent;
        _joinType = joinType;
        _alias = alias;
        _metadata = FluentMetadataCache.GetMetadata<T3>();
    }

    public IJoinedQuery3<T1, T2, T3> On<TLeftKey, TRightKey>(Expression<Func<T1, TLeftKey>> leftKey, Expression<Func<T3, TRightKey>> rightKey)
    {
        var leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        var rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        var leftColumn = GetColumnName(FluentMetadataCache.GetMetadata<T1>(), leftProp, _parent.FromAlias);
        var rightColumn = GetColumnName(_metadata, rightProp, _alias);

        var condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery3(condition);
    }

    public IJoinedQuery3<T1, T2, T3> OnFromSecond<TLeftKey, TRightKey>(Expression<Func<T2, TLeftKey>> leftKey, Expression<Func<T3, TRightKey>> rightKey)
    {
        var leftProp = PropertyExtractor.ExtractPropertyName(leftKey);
        var rightProp = PropertyExtractor.ExtractPropertyName(rightKey);

        var leftColumn = GetColumnName(FluentMetadataCache.GetMetadata<T2>(), leftProp, _parent.Joins[0].Alias);
        var rightColumn = GetColumnName(_metadata, rightProp, _alias);

        var condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery3(condition);
    }

    public IJoinedQuery3<T1, T2, T3> On(string leftColumn, string rightColumn)
    {
        var condition = $"{leftColumn} = {rightColumn}";
        return CreateJoinedQuery3(condition);
    }

    public IJoinedQuery3<T1, T2, T3> On(string condition)
    {
        return CreateJoinedQuery3(condition);
    }

    private JoinedQuery3Builder<T1, T2, T3> CreateJoinedQuery3(string onCondition)
    {
        var joinInfo = new JoinInfo(
            _joinType,
            _metadata.TableName,
            _metadata.SchemaName,
            _alias,
            onCondition);

        _parent.AddJoin(joinInfo);
        return new JoinedQuery3Builder<T1, T2, T3>(_parent);
    }

    private string GetColumnName(EntityMetadata metadata, string propertyName, string? alias)
    {
        var columns = metadata.Columns;
        string columnName = propertyName;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Property.Name == propertyName)
            {
                columnName = columns[i].ColumnName;
                break;
            }
        }

        var escaped = _parent.Dialect.EscapeColumnName(columnName);
        var prefix = alias ?? metadata.TableName;
        return $"{prefix}.{escaped}";
    }
}

/// <summary>
/// Query builder for 3-way joined queries.
/// </summary>
internal sealed class JoinedQuery3Builder<T1, T2, T3> : IJoinedQuery3<T1, T2, T3>
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    private readonly JoinedQueryBuilder<T1, T2> _parent;

    public JoinedQuery3Builder(JoinedQueryBuilder<T1, T2> parent)
    {
        _parent = parent;
    }

    public IJoinedQuery3<T1, T2, T3> Where(Expression<Func<T1, T2, T3, bool>> predicate)
    {
        var visitor = new JoinExpressionVisitor3<T1, T2, T3>(
            _parent.Dialect,
            _parent.FromAlias,
            _parent.Joins[0].Alias,
            _parent.Joins[1].Alias);

        var sql = visitor.Translate(predicate);
        _parent.AddWhereCondition(WhereCondition.Expression(sql, LogicalOperator.None));
        return this;
    }

    public IJoinedQuery3<T1, T2, T3> Where(string condition)
    {
        // Use raw SQL for now
        return this;
    }

    public List<T1> Select()
    {
        return _parent.Select();
    }

    public List<(T1, T2, T3)> SelectAll()
    {
        var t1Metadata = FluentMetadataCache.GetMetadata<T1>();
        var t2Metadata = FluentMetadataCache.GetMetadata<T2>();
        var t3Metadata = FluentMetadataCache.GetMetadata<T3>();

        var t1Columns = _parent.GetPrefixedColumnsWithAlias(t1Metadata, _parent.FromAlias, "t1_");
        var t2Columns = _parent.GetPrefixedColumnsWithAlias(t2Metadata, _parent.Joins[0].Alias, "t2_");
        
        // Find the second join (for T3)
        // JoinClause3Builder.CreateJoinedQuery3 adds the join to _parent.Joins
        var t3Columns = _parent.GetPrefixedColumnsWithAlias(t3Metadata, _parent.Joins[1].Alias, "t3_");
        var allColumns = t1Columns.Concat(t2Columns).Concat(t3Columns).ToArray();

        var sql = _parent.BuildSelectSql(allColumns);
        var results = new List<(T1, T2, T3)>();

        using var command = _parent.Connection.CreateCommand();
        command.CommandText = sql;
        _parent.BindParameters(command);

        var wasClosed = _parent.Connection.State == ConnectionState.Closed;
        if (wasClosed) _parent.Connection.Open();
        try
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var t1Obj = JoinedQueryBuilder<T1, T2>.MapEntity<T1>(t1Metadata, reader, "t1_");
                var t2Obj = JoinedQueryBuilder<T1, T2>.MapEntity<T2>(t2Metadata, reader, "t2_");
                var t3Obj = JoinedQueryBuilder<T1, T2>.MapEntity<T3>(t3Metadata, reader, "t3_");
                results.Add((t1Obj, t2Obj, t3Obj));
            }
        }
        finally
        {
            if (wasClosed) _parent.Connection.Close();
        }

        return results;
    }

    public string ToSql()
    {
        return _parent.ToSql();
    }
}
