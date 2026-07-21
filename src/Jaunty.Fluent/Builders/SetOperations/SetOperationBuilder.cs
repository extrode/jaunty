using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

using Jaunty.Dialects;
using Jaunty.Fluent.Expressions;
using Jaunty.Fluent.Internals;
using Jaunty.Internals.Entity;

namespace Jaunty.Fluent;

/// <summary>
/// Type of set operation.
/// </summary>
internal enum SetOperationType
{
    Union,
    UnionAll,
    Except,
    Intersect
}

/// <summary>
/// Represents a query component in a set operation chain.
/// </summary>
internal readonly struct SetOperationComponent
{
    public SetOperationType OperationType { get; }
    public string Sql { get; }
    public ParameterCollection Parameters { get; }

    public SetOperationComponent(SetOperationType operationType, string sql, ParameterCollection parameters)
    {
        OperationType = operationType;
        Sql = sql;
        Parameters = parameters;
    }
}

/// <summary>
/// Builds set operation queries (UNION, UNION ALL, EXCEPT, INTERSECT).
/// </summary>
internal sealed class SetOperationBuilder<T> : ISetOperationClause<T>, ISetOperationOrderByClause<T>
    where T : new()
{
    private readonly IDbConnection _connection;
    private readonly ISqlDialect _dialect;
    private readonly EntityMetadata _metadata;
    private readonly string _firstQuerySql;
    private readonly ParameterCollection _firstQueryParameters;
    private readonly List<SetOperationComponent> _operations = new();
    private readonly List<OrderByColumn> _orderByColumns = new();
    private int? _take;
    private int? _skip;
    private int _operationCount = 1; // Starts at 1 because first query uses 0

    internal SetOperationBuilder(
        IDbConnection connection,
        ISqlDialect dialect,
        string firstQuerySql,
        ParameterCollection firstQueryParameters)
    {
        _connection = connection;
        _dialect = dialect;
        _metadata = FluentMetadataCache.GetMetadata<T>();

        // Rename first query parameters with prefix "p0_"
        (string? renamedSql, ParameterCollection? renamedParams) = RenameParameters(firstQuerySql, firstQueryParameters, "p0");
        _firstQuerySql = renamedSql;
        _firstQueryParameters = renamedParams;
    }

    #region Set Operations

    public ISetOperationClause<T> Union(IQueryTerminal<T> other)
    {
        AddOperation(SetOperationType.Union, other);
        return this;
    }

    public ISetOperationClause<T> UnionAll(IQueryTerminal<T> other)
    {
        AddOperation(SetOperationType.UnionAll, other);
        return this;
    }

    public ISetOperationClause<T> Except(IQueryTerminal<T> other)
    {
        AddOperation(SetOperationType.Except, other);
        return this;
    }

    public ISetOperationClause<T> Intersect(IQueryTerminal<T> other)
    {
        AddOperation(SetOperationType.Intersect, other);
        return this;
    }

    private void AddOperation(SetOperationType operationType, IQueryTerminal<T> other)
    {
        var sql = other.ToSql();
        ParameterCollection parameters = ExtractParameters(other);

        // Rename parameters with unique prefix
        var prefix = $"p{_operationCount}";
        (string? renamedSql, ParameterCollection? renamedParams) = RenameParameters(sql, parameters, prefix);

        _operations.Add(new SetOperationComponent(operationType, renamedSql, renamedParams));
        _operationCount++;
    }

    private static ParameterCollection ExtractParameters(IQueryTerminal<T> query)
    {
        // SetOperationBuilder<T> does not implement IQueryTerminal<T>, so it can never
        // reach this method - only QueryBuilder<T> (or another IQueryTerminal<T>
        // implementation) can.
        if (query is QueryBuilder<T> qb)
        {
            return qb.GetParameters();
        }
        return new ParameterCollection();
    }

    /// <summary>
    /// Renames all parameters in the SQL and parameter collection with the given prefix.
    /// </summary>
    private static (string Sql, ParameterCollection Parameters) RenameParameters(
        string sql,
        ParameterCollection parameters,
        string prefix)
    {
        var renamedParams = new ParameterCollection();
        var renamedSql = sql;

        foreach ((string? name, object? value) in parameters.GetAll())
        {
            // Detect prefix from the parameter name itself (@ or $)
            var paramPrefix = name.Length > 0 && name[0] is '@' or '$' ? name[0].ToString() : "@";
            var baseName = name.TrimStart('@').TrimStart('$');
            var newName = $"{paramPrefix}{prefix}_{baseName}";

            // Replace in SQL - use word boundary to avoid partial matches
            var pattern = $@"{Regex.Escape(paramPrefix)}{Regex.Escape(baseName)}(?![a-zA-Z0-9_])";
            renamedSql = Regex.Replace(renamedSql, pattern, newName);

            renamedParams.Add(newName, value);
        }

        return (renamedSql, renamedParams);
    }

    #endregion

    #region ORDER BY

    ISetOperationOrderByClause<T> ISetOperationClause<T>.OrderBy(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        var columnName = GetColumnNameFromProperty(propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    ISetOperationOrderByClause<T> ISetOperationClause<T>.OrderByDescending(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        var columnName = GetColumnNameFromProperty(propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    ISetOperationOrderByClause<T> ISetOperationClause<T>.OrderBy(string column)
    {
        _orderByColumns.Add(new OrderByColumn(column, descending: false));
        return this;
    }

    ISetOperationOrderByClause<T> ISetOperationClause<T>.OrderByDescending(string column)
    {
        _orderByColumns.Add(new OrderByColumn(column, descending: true));
        return this;
    }

    public ISetOperationOrderByClause<T> ThenBy(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        var columnName = GetColumnNameFromProperty(propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: false));
        return this;
    }

    public ISetOperationOrderByClause<T> ThenByDescending(Expression<Func<T, object?>> keySelector)
    {
        var propertyName = PropertyExtractor.ExtractOrderByProperty(keySelector);
        var columnName = GetColumnNameFromProperty(propertyName);
        _orderByColumns.Add(new OrderByColumn(columnName, descending: true));
        return this;
    }

    public ISetOperationOrderByClause<T> ThenBy(string column)
    {
        _orderByColumns.Add(new OrderByColumn(column, descending: false));
        return this;
    }

    public ISetOperationOrderByClause<T> ThenByDescending(string column)
    {
        _orderByColumns.Add(new OrderByColumn(column, descending: true));
        return this;
    }

    #endregion

    #region Take/Skip

    ISetOperationClause<T> ISetOperationClause<T>.Take(int count)
    {
        _take = count;
        return this;
    }

    ISetOperationClause<T> ISetOperationClause<T>.Skip(int count)
    {
        _skip = count;
        return this;
    }

    ISetOperationOrderByClause<T> ISetOperationOrderByClause<T>.Take(int count)
    {
        _take = count;
        return this;
    }

    ISetOperationOrderByClause<T> ISetOperationOrderByClause<T>.Skip(int count)
    {
        _skip = count;
        return this;
    }

    #endregion

    #region Terminal Operations (sync)

    public List<T> Select()
    {
        var sql = ToSql();
        return _connection.Query<T>(sql, GetAllParameters().ToParameterObject()!);
    }

    public T SelectFirst()
    {
        var original = _take;
        _take = 1;
        var sql = ToSql();
        _take = original;
        return _connection.QueryFirst<T>(sql, GetAllParameters().ToParameterObject()!);
    }

    public T? SelectFirstOrDefault()
    {
        var original = _take;
        _take = 1;
        var sql = ToSql();
        _take = original;
        return _connection.QueryFirstOrDefault<T>(sql, GetAllParameters().ToParameterObject()!);
    }

    public T SelectSingle()
    {
        var original = _take;
        _take = 2;
        var sql = ToSql();
        _take = original;
        return _connection.QuerySingle<T>(sql, GetAllParameters().ToParameterObject()!);
    }

    public T? SelectSingleOrDefault()
    {
        var original = _take;
        _take = 2;
        var sql = ToSql();
        _take = original;
        return _connection.QuerySingleOrDefault<T>(sql, GetAllParameters().ToParameterObject()!);
    }

    #endregion

    #region Terminal Operations (async)

    public async Task<List<T>> SelectAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var sql = ToSql();
        return await dbConn.QueryAsync<T>(sql, GetAllParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectFirstAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var original = _take;
        _take = 1;
        var sql = ToSql();
        _take = original;
        return await dbConn.QueryFirstAsync<T>(sql, GetAllParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var original = _take;
        _take = 1;
        var sql = ToSql();
        _take = original;
        return await dbConn.QueryFirstOrDefaultAsync<T>(sql, GetAllParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectSingleAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var original = _take;
        _take = 2;
        var sql = ToSql();
        _take = original;
        return await dbConn.QuerySingleAsync<T>(sql, GetAllParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var original = _take;
        _take = 2;
        var sql = ToSql();
        _take = original;
        return await dbConn.QuerySingleOrDefaultAsync<T>(sql, GetAllParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region SQL Generation

    public string ToSql()
    {
        var sb = new StringBuilder(512);

        // First query (already has renamed parameters)
        sb.Append(_firstQuerySql);

        // Chain operations (each with renamed parameters)
        foreach (SetOperationComponent operation in _operations)
        {
            sb.Append(' ');
            sb.Append(GetSetOperationKeyword(operation.OperationType));
            sb.Append(' ');
            sb.Append(operation.Sql);
        }

        // ORDER BY (applies to entire result)
        if (_orderByColumns.Count > 0)
        {
            sb.Append(" ORDER BY ");
            for (int i = 0; i < _orderByColumns.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                OrderByColumn orderBy = _orderByColumns[i];
                sb.Append(_dialect.EscapeColumnName(orderBy.ColumnName));
                if (orderBy.Descending)
                    sb.Append(" DESC");
            }
        }

        // LIMIT/OFFSET
        if (_skip.HasValue || _take.HasValue)
        {
            var baseSql = sb.ToString();
            return _dialect.GetPagingSql(baseSql, _skip ?? 0, _take ?? int.MaxValue);
        }

        return sb.ToString();
    }

    private static string GetSetOperationKeyword(SetOperationType operationType) => operationType switch
    {
        SetOperationType.Union => "UNION",
        SetOperationType.UnionAll => "UNION ALL",
        SetOperationType.Except => "EXCEPT",
        SetOperationType.Intersect => "INTERSECT",
        _ => throw new ArgumentOutOfRangeException(nameof(operationType))
    };

    #endregion

    #region Private Helpers

    /// <summary>
    /// Gets all parameters from first query and all operations (already renamed).
    /// </summary>
    internal ParameterCollection GetAllParameters()
    {
        var combined = new ParameterCollection();

        // Add first query parameters (already renamed)
        foreach ((string? name, object? value) in _firstQueryParameters.GetAll())
        {
            combined.Add(name, value);
        }

        // Add each operation's parameters (already renamed)
        foreach (SetOperationComponent operation in _operations)
        {
            foreach ((string? name, object? value) in operation.Parameters.GetAll())
            {
                combined.Add(name, value);
            }
        }

        return combined;
    }

    private string GetColumnNameFromProperty(string propertyName)
    {
        IReadOnlyList<ColumnMetadata> columns = _metadata.Columns;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].PropertyName == propertyName)
                return columns[i].ColumnName;
        }
        return propertyName;
    }

    #endregion
}