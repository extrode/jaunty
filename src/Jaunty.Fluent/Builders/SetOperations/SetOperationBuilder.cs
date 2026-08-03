using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;

using Jaunty.Core;
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
        (string? renamedSql, ParameterCollection? renamedParams) = ParameterRenamer.Rename(firstQuerySql, firstQueryParameters, "p0");
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
        ThrowIfOperandHasOrderingOrPaging(other, sql);
        ParameterCollection parameters = ExtractParameters(other, sql);

        // Rename parameters with unique prefix
        var prefix = $"p{_operationCount}";
        (string? renamedSql, ParameterCollection? renamedParams) = ParameterRenamer.Rename(sql, parameters, prefix);

        _operations.Add(new SetOperationComponent(operationType, renamedSql, renamedParams));
        _operationCount++;
    }

    // An operand that already has its own ORDER BY/Take/Skip applied would have that
    // ordering/paging spliced verbatim into the middle (or end) of the combined
    // UNION/EXCEPT/INTERSECT statement instead of applying to the combined result - either
    // a syntax error (if this builder's own OrderBy/Take/Skip is also used) or silent
    // semantic corruption (if it isn't, since the operand's clause ends up governing the
    // whole result). Only the outer set-operation chain's OrderBy/Take/Skip is meaningful;
    // reject the operand up front instead of emitting broken or misleading SQL.
    private void ThrowIfOperandHasOrderingOrPaging(IQueryTerminal<T> query, string sql)
    {
        bool hasOrderingOrPaging = query is QueryBuilder<T> qb
            ? qb.HasOrderingOrPaging()
            : CustomOperandHasOrderingOrPaging(sql);

        if (hasOrderingOrPaging)
        {
            throw new NotSupportedException(
                "Union/UnionAll/Except/Intersect operands must not have their own OrderBy/Take/Skip applied. " +
                "Ordering and paging apply to the combined result - call OrderBy/Take/Skip on the outer " +
                "set-operation chain (after Union/UnionAll/Except/Intersect) instead.");
        }
    }

    // A QueryBuilder<T> reports its own ordering/paging state; a custom IQueryTerminal<T> can only
    // be judged by the SQL it produced. Until AUD-R31 that meant searching for " ORDER BY " alone,
    // so an operand that applied paging *without* ordering - LIMIT/OFFSET/FETCH on the ANSI and
    // MySQL/SQLite/PostgreSQL dialects, TOP on SQL Server - passed the guard and had its paging
    // spliced into the combined statement, where it silently governs the whole result. Matching is
    // whole-word and case-insensitive because custom SQL is hand-written; a column merely
    // containing one of these words (toplevel, limits) is therefore not flagged.
    private static readonly string[] PagingKeywords = ["ORDER BY", "LIMIT", "OFFSET", "FETCH", "TOP"];

    // AUD-R35-191. The scan used to run over the raw text, so a parameter-free operand whose SQL
    // merely contained one of these words inside a string literal, a quoted identifier or a comment -
    // WHERE product_name = 'Top Gun', LIKE '%order by%' - was rejected for ordering it does not have.
    // The word-boundary check AUD-R31-003 added defends against toplevel/limits, not against
    // literals. SqlServerDialect.HasTopLevelOrderBy already solves this in the same repository by
    // masking the uninteresting regions first; this does the same, then runs the existing
    // whole-word search over the masked copy so the boundary rule and the keyword list are unchanged.
    private static bool CustomOperandHasOrderingOrPaging(string sql)
    {
        string scannable = MaskLiteralsAndComments(sql);

        foreach (string keyword in PagingKeywords)
        {
            int from = 0;
            while (from <= scannable.Length - keyword.Length)
            {
                int at = scannable.IndexOf(keyword, from, StringComparison.OrdinalIgnoreCase);
                if (at < 0)
                    break;

                if (IsWordBoundary(scannable, at - 1) && IsWordBoundary(scannable, at + keyword.Length))
                    return true;

                from = at + 1;
            }
        }

        return false;
    }

    /// <summary>
    /// Replaces the contents of string literals, quoted identifiers and comments with spaces.
    /// </summary>
    /// <remarks>
    /// AUD-R35-191. Length is preserved so offsets still line up with the original; only the
    /// characters change. Handles single-quoted literals with the doubled-quote escape, double-quoted
    /// and bracketed identifiers, <c>--</c> line comments and <c>/* */</c> block comments. Backticks
    /// are included because a custom operand may be hand-written MySQL.
    /// </remarks>
    private static string MaskLiteralsAndComments(string sql)
    {
        char[] masked = sql.ToCharArray();

        for (int i = 0; i < masked.Length; i++)
        {
            char c = masked[i];

            if (c == '\'' || c == '"' || c == '`')
            {
                char quote = c;
                i++;

                while (i < masked.Length)
                {
                    if (masked[i] == quote)
                    {
                        if (i + 1 < masked.Length && masked[i + 1] == quote)
                        {
                            masked[i] = ' ';
                            masked[i + 1] = ' ';
                            i += 2;
                            continue;
                        }

                        break;
                    }

                    masked[i] = ' ';
                    i++;
                }

                continue;
            }

            if (c == '[')
            {
                i++;

                while (i < masked.Length && masked[i] != ']')
                {
                    masked[i] = ' ';
                    i++;
                }

                continue;
            }

            if (c == '-' && i + 1 < masked.Length && masked[i + 1] == '-')
            {
                while (i < masked.Length && masked[i] != '\n')
                {
                    masked[i] = ' ';
                    i++;
                }

                continue;
            }

            if (c == '/' && i + 1 < masked.Length && masked[i + 1] == '*')
            {
                while (i < masked.Length && !(masked[i] == '*' && i + 1 < masked.Length && masked[i + 1] == '/'))
                {
                    masked[i] = ' ';
                    i++;
                }

                if (i + 1 < masked.Length)
                {
                    masked[i] = ' ';
                    masked[i + 1] = ' ';
                    i++;
                }

                continue;
            }
        }

        return new string(masked);
    }

    private static bool IsWordBoundary(string sql, int index)
    {
        if (index < 0 || index >= sql.Length)
            return true;

        char c = sql[index];
        return !char.IsLetterOrDigit(c) && c != '_';
    }

    private ParameterCollection ExtractParameters(IQueryTerminal<T> query, string sql)
    {
        // SetOperationBuilder<T> does not implement IQueryTerminal<T>, so it can never
        // reach this method - only QueryBuilder<T> (or another IQueryTerminal<T>
        // implementation) can.
        if (query is QueryBuilder<T> qb)
        {
            return qb.GetParameters();
        }

        // A custom IQueryTerminal<T> has no way to report its bound parameters here, so its
        // SQL must be parameter-free - otherwise its placeholders would be spliced into the
        // combined SQL with no corresponding values ever bound. Fail loudly instead of
        // silently emitting broken SQL, mirroring QueryBuilder<T>.BuildInSubqueryClause's
        // guard for the same class of gap in WhereInSubquery/WhereNotInSubquery.
        if (sql.IndexOf(_dialect.ParameterPrefix, StringComparison.Ordinal) >= 0)
        {
            throw new NotSupportedException(
                $"Union/UnionAll/Except/Intersect only supports merging parameters from " +
                $"a query built via QueryBuilder<{typeof(T).Name}> (e.g. connection.From<{typeof(T).Name}>()...). " +
                $"The provided IQueryTerminal<{typeof(T).Name}> implementation produced " +
                $"parameterized SQL that cannot be safely merged into the combined query.");
        }

        return new ParameterCollection();
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

    public List<T> Select(CommandOptions options)
    {
        var sql = ToSql();
        return _connection.Query<T>(sql, GetAllParameters().ToParameterObject()!, ToTypedOptions<T>(options));
    }

    /// <summary>
    /// Builds the combined statement with <c>_take</c> temporarily clamped to <paramref name="take"/>.
    /// </summary>
    /// <remarks>
    /// AUD-R35-190. The sixteen First/Single terminals each inlined "save <c>_take</c>, set it to 1
    /// or 2, call <see cref="ToSql"/>, restore" with no <c>try</c>/<c>finally</c>. <c>ToSql</c> can
    /// throw on a reachable path - <c>EscapeColumnName</c> runs <c>SqlIdentifierValidator</c>, so a
    /// bad string column handed to the <c>OrderBy(string)</c>/<c>ThenBy(string)</c> overloads throws
    /// from inside the guarded region - and the throw left <c>_take</c> at 1 or 2 permanently, so a
    /// later <c>Select()</c> on the same builder silently returned at most one or two rows of the
    /// combined result. The save/restore pattern itself says a terminal must not mutate the builder.
    /// Same defect and same fix as AUD-R35-187 on <c>QueryBuilder</c>.
    /// </remarks>
    private string ToSqlTaking(int take)
    {
        int? original = _take;
        _take = take;

        try
        {
            return ToSql();
        }
        finally
        {
            _take = original;
        }
    }

    public T SelectFirst()
    {
        var sql = ToSqlTaking(1);
        return _connection.QueryFirst<T>(sql, GetAllParameters().ToParameterObject()!);
    }

    public T SelectFirst(CommandOptions options)
    {
        var sql = ToSqlTaking(1);
        return _connection.QueryFirst<T>(sql, GetAllParameters().ToParameterObject()!, ToTypedOptions<T>(options));
    }

    public T? SelectFirstOrDefault()
    {
        var sql = ToSqlTaking(1);
        return _connection.QueryFirstOrDefault<T>(sql, GetAllParameters().ToParameterObject()!);
    }

    public T? SelectFirstOrDefault(CommandOptions options)
    {
        var sql = ToSqlTaking(1);
        return _connection.QueryFirstOrDefault<T>(sql, GetAllParameters().ToParameterObject()!, ToTypedOptions<T>(options));
    }

    public T SelectSingle()
    {
        var sql = ToSqlTaking(2);
        return _connection.QuerySingle<T>(sql, GetAllParameters().ToParameterObject()!);
    }

    public T SelectSingle(CommandOptions options)
    {
        var sql = ToSqlTaking(2);
        return _connection.QuerySingle<T>(sql, GetAllParameters().ToParameterObject()!, ToTypedOptions<T>(options));
    }

    public T? SelectSingleOrDefault()
    {
        var sql = ToSqlTaking(2);
        return _connection.QuerySingleOrDefault<T>(sql, GetAllParameters().ToParameterObject()!);
    }

    public T? SelectSingleOrDefault(CommandOptions options)
    {
        var sql = ToSqlTaking(2);
        return _connection.QuerySingleOrDefault<T>(sql, GetAllParameters().ToParameterObject()!, ToTypedOptions<T>(options));
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

    public async Task<List<T>> SelectAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var sql = ToSql();
        return await dbConn.QueryAsync<T>(sql, GetAllParameters().ToParameterObject()!, ToTypedOptions<T>(options), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectFirstAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var sql = ToSqlTaking(1);
        return await dbConn.QueryFirstAsync<T>(sql, GetAllParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectFirstAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var sql = ToSqlTaking(1);
        return await dbConn.QueryFirstAsync<T>(sql, GetAllParameters().ToParameterObject()!, ToTypedOptions<T>(options), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var sql = ToSqlTaking(1);
        return await dbConn.QueryFirstOrDefaultAsync<T>(sql, GetAllParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectFirstOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var sql = ToSqlTaking(1);
        return await dbConn.QueryFirstOrDefaultAsync<T>(sql, GetAllParameters().ToParameterObject()!, ToTypedOptions<T>(options), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectSingleAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var sql = ToSqlTaking(2);
        return await dbConn.QuerySingleAsync<T>(sql, GetAllParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T> SelectSingleAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var sql = ToSqlTaking(2);
        return await dbConn.QuerySingleAsync<T>(sql, GetAllParameters().ToParameterObject()!, ToTypedOptions<T>(options), cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectSingleOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var sql = ToSqlTaking(2);
        return await dbConn.QuerySingleOrDefaultAsync<T>(sql, GetAllParameters().ToParameterObject()!, cancellationToken).ConfigureAwait(false);
    }

    public async Task<T?> SelectSingleOrDefaultAsync(CommandOptions options, CancellationToken cancellationToken = default)
    {
        if (_connection is not DbConnection dbConn)
            throw new InvalidOperationException("Async operations require a DbConnection.");

        var sql = ToSqlTaking(2);
        return await dbConn.QuerySingleOrDefaultAsync<T>(sql, GetAllParameters().ToParameterObject()!, ToTypedOptions<T>(options), cancellationToken).ConfigureAwait(false);
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

    private static CommandOptions<TResult> ToTypedOptions<TResult>(CommandOptions options) =>
        new(transaction: options.Transaction, commandTimeout: options.CommandTimeout, commandType: options.CommandType);

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