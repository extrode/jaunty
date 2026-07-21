using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;

using Jaunty.Configuration;
using Jaunty.Internals.Read;

namespace Jaunty.Fluent;

/// <summary>
/// Async select operations for 2-table joins.
/// </summary>
internal partial class JoinedQueryBuilder<TFrom, TJoin>
{
    public async Task<List<TFrom>> SelectAsync(CancellationToken cancellationToken = default)
    {
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = BuildSelectSql(columns);

        return _connection is DbConnection dbConn
            ? await dbConn.QueryPartialAsync<TFrom>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false)
            : throw new NotSupportedException("Async operations require DbConnection.");
    }

    public async Task<List<T>> SelectAsync<T>(CancellationToken cancellationToken = default)
        where T : new()
    {
        if (typeof(T) == typeof(TFrom))
        {
            List<TFrom> result = await SelectAsync(cancellationToken).ConfigureAwait(false);
            return Unsafe.As<List<TFrom>, List<T>>(ref result);
        }

        if (typeof(T) == typeof(TJoin))
        {
            List<TJoin> result = await SelectJoinedAsync(cancellationToken).ConfigureAwait(false);
            return Unsafe.As<List<TJoin>, List<T>>(ref result);
        }

        return await SelectWithMappingAsync<T>(MappingMode.Strict, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<T>> SelectAsync<T>(Func<IDataReader, T> mapper, CancellationToken cancellationToken = default)
    {
        return await SelectWithMapperAsync(mapper, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<(T1, T2)>> SelectAsync<T1, T2>(CancellationToken cancellationToken = default)
        where T1 : new()
        where T2 : new()
    {
        if (typeof(T1) != typeof(TFrom))
            throw new ArgumentException($"T1 must be {typeof(TFrom).Name}, got {typeof(T1).Name}", nameof(T1));

        if (typeof(T2) != typeof(TJoin))
            throw new ArgumentException($"T2 must be {typeof(TJoin).Name}, got {typeof(T2).Name}", nameof(T2));

        List<(TFrom From, TJoin Joined)> result = await SelectBothInternalAsync(cancellationToken).ConfigureAwait(false);
        return Unsafe.As<List<(TFrom, TJoin)>, List<(T1, T2)>>(ref result);
    }

    public async Task<TFrom> SelectFirstAsync(CancellationToken cancellationToken = default)
    {
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 1);

        return _connection is DbConnection dbConn
            ? await dbConn.QueryPartialFirstAsync<TFrom>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false)
            : throw new NotSupportedException("Async operations require DbConnection.");
    }

    public async Task<TFrom?> SelectFirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        string[] columns = GetPrefixedColumns(_fromMetadata, _fromAlias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 1);

        return _connection is DbConnection dbConn
            ? await dbConn.QueryPartialFirstOrDefaultAsync<TFrom>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false)
            : throw new NotSupportedException("Async operations require DbConnection.");
    }

    public async Task<T> SelectFirstAsync<T>(CancellationToken cancellationToken = default)
        where T : new()
    {
        if (typeof(T) == typeof(TFrom))
        {
            TFrom? result = await SelectFirstAsync(cancellationToken).ConfigureAwait(false);
            return Unsafe.As<TFrom, T>(ref result);
        }

        if (typeof(T) == typeof(TJoin))
        {
            TJoin? result = await SelectFirstJoinedAsync(cancellationToken).ConfigureAwait(false);
            return Unsafe.As<TJoin, T>(ref result);
        }

        List<T> results = await SelectWithMappingAsync<T>(MappingMode.Strict, cancellationToken).ConfigureAwait(false);
        return results.Count == 0
            ? throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.")
            : results[0];
    }

    public async Task<T> SelectFirstAsync<T>(
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        List<T> results = await SelectWithMapperAsync(mapper, cancellationToken).ConfigureAwait(false);
        return results.Count == 0
            ? throw new InvalidOperationException($"Sequence contains no elements of type '{typeof(T).Name}'.")
            : results[0];
    }

    public async Task<T?> SelectFirstOrDefaultAsync<T>(CancellationToken cancellationToken = default)
        where T : new()
    {
        if (typeof(T) == typeof(TFrom))
        {
            TFrom? result = await SelectFirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            return Unsafe.As<TFrom?, T?>(ref result);
        }

        if (typeof(T) == typeof(TJoin))
        {
            TJoin? result = await SelectFirstOrDefaultJoinedAsync(cancellationToken).ConfigureAwait(false);
            return Unsafe.As<TJoin?, T?>(ref result);
        }

        List<T> results = await SelectWithMappingAsync<T>(MappingMode.Strict, cancellationToken).ConfigureAwait(false);
        return results.Count > 0 ? results[0] : default;
    }

    public async Task<T?> SelectFirstOrDefaultAsync<T>(
        Func<IDataReader, T> mapper,
        CancellationToken cancellationToken = default)
    {
        List<T> results = await SelectWithMapperAsync(mapper, cancellationToken).ConfigureAwait(false);
        return results.Count > 0 ? results[0] : default;
    }

    public async Task<(TFrom From, TJoin Joined)> SelectFirstBothAsync(CancellationToken cancellationToken = default)
    {
        List<(TFrom From, TJoin Joined)> result = await SelectBothInternalAsync(cancellationToken).ConfigureAwait(false);
        return result.Count == 0
            ? throw new InvalidOperationException($"Sequence contains no elements of type '({typeof(TFrom).Name}, {typeof(TJoin).Name})'.")
            : result[0];
    }

    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        string sql = BuildCountSql();

        return _connection is DbConnection dbConn
            ? await dbConn.QueryScalarAsync<int>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false)
            : throw new NotSupportedException("Async operations require DbConnection.");
    }

    public async Task<long> LongCountAsync(CancellationToken cancellationToken = default)
    {
        string sql = BuildCountSql();

        return _connection is DbConnection dbConn
            ? await dbConn.QueryScalarAsync<long>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false)
            : throw new NotSupportedException("Async operations require DbConnection.");
    }

    private async Task<List<TJoin>> SelectJoinedAsync(CancellationToken cancellationToken = default)
    {
        string[] columns = GetPrefixedColumns(_joinMetadata, _joins[0].Alias);
        string sql = BuildSelectSql(columns);

        return _connection is DbConnection dbConn
            ? await dbConn.QueryPartialAsync<TJoin>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false)
            : throw new NotSupportedException("Async operations require DbConnection.");
    }

    private async Task<TJoin> SelectFirstJoinedAsync(CancellationToken cancellationToken = default)
    {
        string[] columns = GetPrefixedColumns(_joinMetadata, _joins[0].Alias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 1);

        return _connection is DbConnection dbConn
            ? await dbConn.QueryPartialFirstAsync<TJoin>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false)
            : throw new NotSupportedException("Async operations require DbConnection.");
    }

    private async Task<TJoin?> SelectFirstOrDefaultJoinedAsync(CancellationToken cancellationToken = default)
    {
        string[] columns = GetPrefixedColumns(_joinMetadata, _joins[0].Alias);
        string sql = _dialect.GetPagingSql(BuildSelectSql(columns), 0, 1);

        return _connection is DbConnection dbConn
            ? await dbConn.QueryPartialFirstOrDefaultAsync<TJoin>(sql, _parameters.ToParameterObject()!, cancellationToken).ConfigureAwait(false)
            : throw new NotSupportedException("Async operations require DbConnection.");
    }

    private async Task<List<(TFrom From, TJoin Joined)>> SelectBothInternalAsync(CancellationToken cancellationToken = default)
    {
        string[] fromColumns = GetPrefixedColumnsWithAlias(_fromMetadata, _fromAlias, "f_");
        string[] joinColumns = GetPrefixedColumnsWithAlias(_joinMetadata, _joins[0].Alias, "j_");
        string[] allColumns = fromColumns.Concat(joinColumns).ToArray();

        string sql = BuildSelectSql(allColumns);
        var results = new List<(TFrom, TJoin)>();

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        DbCommand command = dbConn.CreateCommand();
        await using var commandDisposer = command.ConfigureAwait(false);
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif
            Dictionary<string, int> ordinals = BuildOrdinalLookup(reader);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                TFrom? fromObj = MapEntity<TFrom>(_fromMetadata, reader, "f_", ordinals);
                TJoin? joinObj = MapEntity<TJoin>(_joinMetadata, reader, "j_", ordinals);
                results.Add((fromObj, joinObj));
            }
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        return results;
    }

    private async Task<List<T>> SelectWithMappingAsync<T>(MappingMode mode, CancellationToken cancellationToken = default) where T : new()
    {
        string sql = BuildSelectAllColumnsSql();
        var results = new List<T>();

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        DbCommand command = dbConn.CreateCommand();
        await using var commandDisposer = command.ConfigureAwait(false);
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif
            EnsureNoAmbiguousColumns(reader);
            Func<DbDataReader, T> mapper = DrDispatcher.Resolve<T>(reader, default, mode);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                results.Add(mapper(reader));
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        return results;
    }

    private async Task<List<T>> SelectWithMapperAsync<T>(Func<IDataReader, T> mapper, CancellationToken cancellationToken = default)
    {
        string sql = BuildSelectAllColumnsSql();
        var results = new List<T>();

        if (_connection is not DbConnection dbConn)
            throw new NotSupportedException("Async operations require DbConnection.");

#if NET8_0_OR_GREATER
        DbCommand command = dbConn.CreateCommand();
        await using var commandDisposer = command.ConfigureAwait(false);
#else
        using DbCommand command = dbConn.CreateCommand();
#endif
        command.CommandText = sql;
        BindParameters(command);

        bool wasClosed = dbConn.State == ConnectionState.Closed;
        if (wasClosed)
            await dbConn.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
#if NET8_0_OR_GREATER
            DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using var readerDisposer = reader.ConfigureAwait(false);
#else
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#endif

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                results.Add(mapper(reader));
        }
        finally
        {
            if (wasClosed)
            {
#if NET8_0_OR_GREATER
                await dbConn.CloseAsync().ConfigureAwait(false);
#else
                dbConn.Close();
#endif
            }
        }

        return results;
    }
}
